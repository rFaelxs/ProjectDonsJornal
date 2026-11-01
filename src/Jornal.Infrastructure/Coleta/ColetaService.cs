using Jornal.Core.Catalogo;
using Jornal.Core.Coleta;
using Jornal.Core.Comum;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jornal.Infrastructure.Coleta;

/// <summary>
/// Lê todos os feeds ativos e grava os itens novos (RF13). Um feed que falha gera uma linha
/// em <c>coleta_falha</c> e a coleta continua; o ciclo só aborta se todos falharem (RF17).
/// </summary>
public class ColetaService(
    JornalDbContext db,
    IFeedReader leitor,
    IOptions<JornalOptions> options,
    ILogger<ColetaService> log) : IColetaService
{
    /// <summary>Preenchido pelo ciclo diário, para as falhas apontarem para a execução.</summary>
    public Guid? CicloId { get; set; }

    public async Task<ColetaResumo> ColetarAsync(CancellationToken ct)
    {
        var opcoes = options.Value.Coleta;

        var feeds = await db.Feeds
            .Where(f => f.Ativo)
            .Include(f => f.Categoria)
            .AsNoTracking()
            .ToListAsync(ct);

        if (feeds.Count == 0)
        {
            log.LogWarning("Nenhum feed ativo. Rode 'jornal validar-feeds --atualizar'.");
            return new ColetaResumo(0, 0, 0, 0, 0);
        }

        // Leitura em paralelo, no máximo N ao mesmo tempo (spec 04). A escrita no banco
        // fica fora do paralelo: um DbContext não é seguro para uso concorrente.
        var limite = new SemaphoreSlim(opcoes.ParalelismoMaximo);
        var leituras = await Task.WhenAll(feeds.Select(async feed =>
        {
            await limite.WaitAsync(ct);
            try
            {
                return (Feed: feed, Resultado: await leitor.ReadAsync(feed, ct));
            }
            finally
            {
                limite.Release();
            }
        }));

        var agora = DateTimeOffset.UtcNow;
        var comFalha = 0;
        var itensNovos = 0;
        var ignoradosPorLink = 0;

        // Hashes já conhecidos, para a etapa 1 da deduplicação (RF15). Carregados de uma vez:
        // consultar item por item seria uma ida ao banco por notícia.
        var hashesDaLeitura = leituras
            .SelectMany(l => l.Resultado.Itens)
            .Select(i => i.LinkHash)
            .Distinct()
            .ToList();

        var hashesExistentes = hashesDaLeitura.Count == 0
            ? []
            : (await db.Itens
                .Where(i => hashesDaLeitura.Contains(i.LinkHash))
                .Select(i => i.LinkHash)
                .ToListAsync(ct))
              .ToHashSet();

        foreach (var (feed, resultado) in leituras)
        {
            var rastreado = await db.Feeds.FirstAsync(f => f.Id == feed.Id, ct);

            if (resultado.Falha is { } falha)
            {
                comFalha++;
                rastreado.FalhasConsecutivas++;

                db.ColetaFalhas.Add(new ColetaFalha
                {
                    FeedId = feed.Id,
                    CicloId = CicloId,
                    OcorreuEm = agora,
                    Tipo = falha.Tipo,
                    Mensagem = falha.Mensagem
                });

                // 3 falhas consecutivas desativam o feed (spec 02).
                if (rastreado.FalhasConsecutivas >= 3 && rastreado.Ativo)
                {
                    rastreado.Ativo = false;
                    log.LogWarning(
                        "Feed {Veiculo} desativado após {Falhas} falhas consecutivas",
                        feed.NomeVeiculo, rastreado.FalhasConsecutivas);
                }

                continue;
            }

            // Sucesso: zera o contador e guarda os cabeçalhos de cache.
            rastreado.FalhasConsecutivas = 0;
            rastreado.UltimaColetaEm = agora;
            rastreado.ETag = resultado.ETag;
            rastreado.UltimoModificadoEm = resultado.UltimoModificadoEm;

            if (resultado.NaoModificado) continue;

            // Na primeira coleta de um feed, ignora o que é antigo, para não poluir a
            // janela de 24h da recuperação (spec 04).
            var primeiraColeta = feed.UltimaColetaEm is null;
            var corteIdade = agora.AddDays(-opcoes.IdadeMaximaDiasPrimeiraColeta);

            foreach (var lido in resultado.Itens)
            {
                if (primeiraColeta && lido.PublicadoEm < corteIdade) continue;

                // Etapa 1 da deduplicação: mesmo link, nem grava (RF15).
                if (!hashesExistentes.Add(lido.LinkHash))
                {
                    ignoradosPorLink++;
                    continue;
                }

                db.Itens.Add(new Item
                {
                    FeedId = feed.Id,
                    CategoriaId = feed.CategoriaId,
                    Fonte = feed.NomeVeiculo,
                    Titulo = lido.Titulo,
                    Resumo = lido.Resumo,
                    Link = lido.Link,
                    LinkHash = lido.LinkHash,
                    PublicadoEm = lido.PublicadoEm,
                    DataEstimada = lido.DataEstimada,
                    ColetadoEm = agora
                });

                itensNovos++;
            }
        }

        await db.SaveChangesAsync(ct);

        var resumo = new ColetaResumo(
            FeedsLidos: feeds.Count,
            FeedsComFalha: comFalha,
            ItensNovos: itensNovos,
            ItensIgnoradosPorLink: ignoradosPorLink,
            // A etapa 2 (similaridade) acontece na indexação, que tem os embeddings.
            ItensMarcadosDuplicata: 0);

        log.LogInformation(
            "Coleta: {Lidos} feeds, {Falhas} falhas, {Novos} itens novos, {Ignorados} por link",
            resumo.FeedsLidos, resumo.FeedsComFalha, resumo.ItensNovos,
            resumo.ItensIgnoradosPorLink);

        return resumo;
    }
}
