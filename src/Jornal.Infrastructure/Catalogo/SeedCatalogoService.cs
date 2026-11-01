using System.Security.Cryptography;
using System.Text;
using Jornal.Core.Catalogo;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Jornal.Infrastructure.Catalogo;

/// <summary>
/// Carrega o <c>catalogo.json</c> no banco (spec 02). Upsert por <c>slug</c>, e o embedding
/// só é regerado quando o texto que o origina muda — é o que torna o seed idempotente (CA-02.1).
/// </summary>
public class SeedCatalogoService(
    JornalDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> gerador,
    ILogger<SeedCatalogoService> log) : ISeedCatalogo
{
    public async Task<SeedResumo> ExecutarAsync(CatalogoArquivo catalogo, CancellationToken ct)
    {
        var categoriasCriadas = 0;
        var categoriasAtualizadas = 0;
        var subtemasCriados = 0;
        var subtemasAtualizados = 0;
        var feedsCriados = 0;
        var feedsIgnorados = 0;

        // Textos que precisam de embedding novo, acumulados para uma chamada em lote.
        var pendentes = new List<EmbeddingPendente>();

        foreach (var arquivo in catalogo.Categorias)
        {
            var categoria = await db.Categorias
                .Include(c => c.Subtemas)
                .Include(c => c.Feeds)
                .FirstOrDefaultAsync(c => c.Slug == arquivo.Slug, ct);

            if (categoria is null)
            {
                categoria = new Categoria
                {
                    Slug = arquivo.Slug,
                    Nome = arquivo.Nome,
                    Descricao = arquivo.Descricao
                };
                db.Categorias.Add(categoria);
                categoriasCriadas++;
            }
            else if (categoria.Nome != arquivo.Nome || categoria.Descricao != arquivo.Descricao)
            {
                categoria.Nome = arquivo.Nome;
                categoria.Descricao = arquivo.Descricao;
                categoriasAtualizadas++;
            }

            AgendarEmbedding(pendentes, categoria.Nome, categoria.Descricao,
                categoria.EmbeddingHash,
                (vetor, hash) => { categoria.Embedding = vetor; categoria.EmbeddingHash = hash; });

            foreach (var subArquivo in arquivo.Subtemas)
            {
                var subtema = categoria.Subtemas.FirstOrDefault(s => s.Slug == subArquivo.Slug);

                if (subtema is null)
                {
                    subtema = new Subtema
                    {
                        Slug = subArquivo.Slug,
                        Nome = subArquivo.Nome,
                        Descricao = subArquivo.Descricao
                    };
                    categoria.Subtemas.Add(subtema);
                    subtemasCriados++;
                }
                else if (subtema.Nome != subArquivo.Nome || subtema.Descricao != subArquivo.Descricao)
                {
                    subtema.Nome = subArquivo.Nome;
                    subtema.Descricao = subArquivo.Descricao;
                    subtemasAtualizados++;
                }

                AgendarEmbedding(pendentes, subtema.Nome, subtema.Descricao,
                    subtema.EmbeddingHash,
                    (vetor, hash) => { subtema.Embedding = vetor; subtema.EmbeddingHash = hash; });
            }

            foreach (var feedArquivo in arquivo.Feeds)
            {
                // URL ainda não levantada: não entra no banco, para não poluir o catálogo.
                if (feedArquivo.UrlPendente)
                {
                    feedsIgnorados++;
                    continue;
                }

                if (categoria.Feeds.Any(f => f.Url == feedArquivo.Url)) continue;

                // Criado inativo de propósito: só o validador liga um feed (RN08).
                categoria.Feeds.Add(new Feed
                {
                    NomeVeiculo = feedArquivo.Veiculo,
                    Url = feedArquivo.Url,
                    Ativo = false
                });
                feedsCriados++;
            }
        }

        var embeddingsGerados = await GerarEmbeddingsAsync(pendentes, ct);
        await db.SaveChangesAsync(ct);

        var resumo = new SeedResumo(
            categoriasCriadas, categoriasAtualizadas,
            subtemasCriados, subtemasAtualizados,
            feedsCriados, feedsIgnorados, embeddingsGerados);

        log.LogInformation(
            "Seed: {Criadas} categorias novas, {Atualizadas} atualizadas, {Feeds} feeds, " +
            "{Embeddings} embeddings gerados, {Ignorados} feeds sem URL",
            categoriasCriadas, categoriasAtualizadas, feedsCriados,
            embeddingsGerados, feedsIgnorados);

        return resumo;
    }

    /// <summary>Um embedding a gerar: o texto, o hash dele e onde guardar o resultado.</summary>
    private record EmbeddingPendente(string Texto, string Hash, Action<float[], string> Aplicar);

    /// <summary>
    /// Só agenda se o hash do texto mudou. É este desvio que faz a segunda execução do seed
    /// não gastar nenhuma chamada ao gerador (CA-02.1).
    /// </summary>
    private static void AgendarEmbedding(
        List<EmbeddingPendente> pendentes,
        string nome,
        string descricao,
        string? hashAtual,
        Action<float[], string> aplicar)
    {
        // O embedding sai de "{nome}: {descricao}" — o nome sozinho gera vetor pobre (RF09).
        var texto = $"{nome}: {descricao}";
        var hash = Sha256(texto);

        if (hash == hashAtual) return;

        pendentes.Add(new EmbeddingPendente(texto, hash, aplicar));
    }

    private async Task<int> GerarEmbeddingsAsync(
        List<EmbeddingPendente> pendentes, CancellationToken ct)
    {
        if (pendentes.Count == 0) return 0;

        var textos = pendentes.Select(p => p.Texto).ToList();
        var vetores = await gerador.GenerateAsync(textos, cancellationToken: ct);

        if (vetores.Count != pendentes.Count)
            throw new InvalidOperationException(
                $"O gerador devolveu {vetores.Count} embeddings para {pendentes.Count} textos.");

        for (var i = 0; i < pendentes.Count; i++)
        {
            var vetor = vetores[i].Vector.ToArray();

            if (vetor.Length != JornalDbContext.DimensoesEmbedding)
                throw new InvalidOperationException(
                    $"O modelo devolveu {vetor.Length} dimensões, mas o schema espera " +
                    $"{JornalDbContext.DimensoesEmbedding}. Confira Jornal:Ai:Embedding:Model.");

            pendentes[i].Aplicar(vetor, pendentes[i].Hash);
        }

        return pendentes.Count;
    }

    private static string Sha256(string texto) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
}
