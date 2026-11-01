using Jornal.Core.Coleta;
using Jornal.Core.Comum;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;

namespace Jornal.Infrastructure.Coleta;

/// <summary>
/// Gera embeddings e trechos dos itens pendentes (RF14) e aplica a etapa 2 da deduplicação,
/// por similaridade de conteúdo (RF15).
///
/// Idempotente: processa só itens sem trechos e sem <c>duplicata_de</c>, então rodar duas
/// vezes não duplica nada.
/// </summary>
public class Indexador(
    JornalDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> gerador,
    IOptions<JornalOptions> options,
    ILogger<Indexador> log) : IIndexador
{
    public async Task<int> IndexarPendentesAsync(CancellationToken ct)
    {
        var opcoes = options.Value;

        var pendentes = await db.Itens
            .Where(i => i.DuplicataDe == null && i.Trechos.Count == 0)
            .OrderBy(i => i.PublicadoEm)
            .ToListAsync(ct);

        if (pendentes.Count == 0)
        {
            log.LogInformation("Nenhum item pendente de indexação.");
            return 0;
        }

        log.LogInformation("Indexando {Total} itens pendentes", pendentes.Count);

        var trechosGravados = 0;
        var duplicatas = 0;

        // Em lotes, porque o gerador recebe vários textos por chamada (spec 04).
        foreach (var lote in pendentes.Chunk(opcoes.Ai.Embedding.TamanhoLote))
        {
            ct.ThrowIfCancellationRequested();

            // Cada item pode virar mais de um trecho; o mapa liga texto → item.
            var textos = new List<string>();
            var donos = new List<(Item Item, int Ordem)>();

            foreach (var item in lote)
            {
                var divididos = DivisorDeTrechos.Dividir(item.Titulo, item.Resumo);
                for (var ordem = 0; ordem < divididos.Count; ordem++)
                {
                    textos.Add(divididos[ordem]);
                    donos.Add((item, ordem));
                }
            }

            if (textos.Count == 0) continue;

            var vetores = await gerador.GenerateAsync(textos, cancellationToken: ct);

            if (vetores.Count != textos.Count)
                throw new InvalidOperationException(
                    $"O gerador devolveu {vetores.Count} embeddings para {textos.Count} textos.");

            // Agrupa por item: a decisão de duplicata é por item, não por trecho.
            for (var i = 0; i < donos.Count; i++)
            {
                var (item, ordem) = donos[i];
                var vetor = vetores[i].Vector.ToArray();

                if (vetor.Length != JornalDbContext.DimensoesEmbedding)
                    throw new InvalidOperationException(
                        $"O modelo devolveu {vetor.Length} dimensões, mas o schema espera " +
                        $"{JornalDbContext.DimensoesEmbedding}. Confira Jornal:Ai:Embedding:Model.");

                // Etapa 2 da deduplicação, avaliada no primeiro trecho de cada item (RF15).
                if (ordem == 0)
                {
                    var original = await AcharMuitoSimilarAsync(vetor, item, ct);
                    if (original is { } originalId)
                    {
                        item.DuplicataDe = originalId;
                        duplicatas++;
                        log.LogDebug(
                            "Item {Titulo} marcado como duplicata de {Original}",
                            item.Titulo, originalId);
                        continue;
                    }
                }

                // Item marcado como duplicata não gera trecho nenhum.
                if (item.DuplicataDe is not null) continue;

                db.Trechos.Add(new Trecho
                {
                    ItemId = item.Id,
                    Ordem = ordem,
                    Texto = textos[i],
                    Embedding = vetor,
                    PublicadoEm = item.PublicadoEm,
                    CategoriaId = item.CategoriaId,
                    Fonte = item.Fonte
                });

                trechosGravados++;
            }

            await db.SaveChangesAsync(ct);
        }

        log.LogInformation(
            "Indexação: {Trechos} trechos gravados, {Duplicatas} itens marcados como duplicata",
            trechosGravados, duplicatas);

        return trechosGravados;
    }

    /// <summary>
    /// Procura um trecho já indexado com similaridade de cosseno ≥ <c>LimiarDuplicata</c>
    /// dentro da janela de comparação (RF15).
    ///
    /// SQL cru porque é consulta vetorial: o operador <c>&lt;=&gt;</c> do pgvector é o que
    /// usa o índice HNSW, e nenhuma tradução do EF chega nele.
    /// </summary>
    private async Task<Guid?> AcharMuitoSimilarAsync(float[] vetor, Item item, CancellationToken ct)
    {
        var opcoes = options.Value.Coleta;
        var desde = item.ColetadoEm.AddHours(-opcoes.DuplicataJanelaHoras);

        var conexao = db.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
            await conexao.OpenAsync(ct);

        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = """
            SELECT t.item_id, 1 - (t.embedding <=> @vetor) AS sim
            FROM trecho t
            WHERE t.publicado_em >= @desde
              AND t.item_id <> @itemId
            ORDER BY t.embedding <=> @vetor
            LIMIT 1
            """;

        cmd.Parameters.Add(new NpgsqlParameter("vetor", new Vector(vetor)));
        cmd.Parameters.Add(new NpgsqlParameter("desde", desde));
        cmd.Parameters.Add(new NpgsqlParameter("itemId", item.Id));

        await using var leitor = await cmd.ExecuteReaderAsync(ct);
        if (!await leitor.ReadAsync(ct)) return null;

        var itemId = leitor.GetGuid(0);
        var similaridade = leitor.GetDouble(1);

        // Entre 0.82 e 0.93 não é duplicata: vira o mesmo assunto na recuperação (RF20),
        // o que permite citar várias fontes. Só acima de 0.93 é a mesma notícia.
        return similaridade >= opcoes.LimiarDuplicata ? itemId : null;
    }
}
