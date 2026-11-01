using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jornal.Infrastructure.Feeds;

/// <summary>
/// Orquestra o comando <c>validar-feeds</c> (RF18). Grava uma linha em
/// <c>feed_validacao</c> por execução e, com <c>--atualizar</c>, liga ou desliga
/// <c>feed.ativo</c> conforme o resultado.
/// </summary>
public class ValidacaoFeedsService(
    JornalDbContext db,
    IFeedValidator validador,
    IOptions<JornalOptions> options,
    ILogger<ValidacaoFeedsService> log) : IValidacaoFeeds
{
    public async Task<ValidacaoFeedsRelatorio> ValidarAsync(
        string? categoriaSlug, bool atualizar, CancellationToken ct)
    {
        var consulta = db.Feeds.Include(f => f.Categoria).AsQueryable();

        if (!string.IsNullOrWhiteSpace(categoriaSlug))
            consulta = consulta.Where(f => f.Categoria!.Slug == categoriaSlug);

        var feeds = await consulta.OrderBy(f => f.Categoria!.Slug)
                                  .ThenBy(f => f.NomeVeiculo)
                                  .ToListAsync(ct);

        if (feeds.Count == 0)
            log.LogWarning("Nenhum feed para validar. O seed já rodou?");

        var validados = new List<FeedValidado>(feeds.Count);
        var agora = DateTimeOffset.UtcNow;

        foreach (var feed in feeds)
        {
            ct.ThrowIfCancellationRequested();

            var resultado = await validador.ValidateAsync(feed.Url, ct);

            db.FeedValidacoes.Add(new FeedValidacao
            {
                FeedId = feed.Id,
                ExecutadoEm = agora,
                HttpStatus = resultado.HttpStatus,
                Formato = resultado.Formato,
                ItensRecentes = resultado.ItensRecentes,
                Aprovado = resultado.Aprovado,
                Motivo = resultado.Motivo
            });

            var alterouAtivo = false;
            if (atualizar)
            {
                alterouAtivo = feed.Ativo != resultado.Aprovado;
                feed.Ativo = resultado.Aprovado;
                feed.ValidadoEm = agora;

                // Aprovar zera o contador: o feed voltou a responder.
                if (resultado.Aprovado) feed.FalhasConsecutivas = 0;
            }

            validados.Add(new FeedValidado(
                feed.Id, feed.Categoria!.Slug, feed.NomeVeiculo, feed.Url, resultado, alterouAtivo));
        }

        await db.SaveChangesAsync(ct);

        return new ValidacaoFeedsRelatorio(validados, await CategoriasAbaixoDoMinimoAsync(ct));
    }

    /// <summary>
    /// RN08: a spec 02 exige que o comando liste, no fim, as categorias com menos de 2 feeds
    /// ativos. Elas ficam indisponíveis para escolha no onboarding (CA-02.4).
    /// </summary>
    private async Task<List<CategoriaSemFeedsSuficientes>> CategoriasAbaixoDoMinimoAsync(
        CancellationToken ct)
    {
        var minimo = options.Value.Perfil.MinimoFeedsAtivosPorCategoria;

        return await db.Categorias
            .Where(c => c.Ativa)
            .Select(c => new CategoriaSemFeedsSuficientes(
                c.Slug, c.Nome, c.Feeds.Count(f => f.Ativo), minimo))
            .Where(x => x.FeedsAtivos < minimo)
            .OrderBy(x => x.Slug)
            .ToListAsync(ct);
    }
}
