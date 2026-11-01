using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jornal.Infrastructure.Feeds;

/// <summary>
/// Implementa os 4 passos do validador da spec 02. Nenhuma URL entra como ativa sem passar
/// por aqui (RN08).
/// </summary>
public class FeedValidator(
    HttpClient http,
    IOptions<JornalOptions> options,
    ILogger<FeedValidator> log) : IFeedValidator
{
    /// <summary>Nome do cliente HTTP configurado em <see cref="ServicosInfraestrutura"/>.</summary>
    public const string ClienteHttp = "feeds";

    /// <summary>Passo 3: itens com data nos últimos 7 dias.</summary>
    private const int DiasParaItemRecente = 7;
    private const int MinimoItensRecentes = 3;

    /// <summary>Passo 4: fração de itens que precisa ter título, link e data.</summary>
    private const double FracaoMinimaCamposCompletos = 0.80;

    public async Task<FeedValidationResult> ValidateAsync(string url, CancellationToken ct)
    {
        // Passo 1 — responde?
        var (conteudo, status, falha) = await BaixarAsync(url, ct);
        if (falha is not null)
        {
            log.LogInformation("Feed {Url} reprovado no passo 1: {Motivo}", url, falha);
            return FeedValidationResult.Reprovado(falha, status);
        }

        // Passo 2 — é RSS 2.0 ou Atom 1.0?
        var leitura = LeitorSindicacao.Ler(conteudo!);
        if (leitura is null)
        {
            // A spec permite uma tentativa de sanitização (BOM, entidades HTML).
            leitura = LeitorSindicacao.Ler(LeitorSindicacao.Sanitizar(conteudo!));
            if (leitura is not null)
                log.LogInformation("Feed {Url} só foi lido após sanitizar o XML", url);
        }

        if (leitura is null)
        {
            log.LogInformation("Feed {Url} reprovado no passo 2: formato_invalido", url);
            return FeedValidationResult.Reprovado("formato_invalido", status);
        }

        if (leitura.UsouFallback)
            log.LogInformation(
                "Feed {Url} exigiu o leitor tolerante: o Syndication recusou o XML", url);

        var total = leitura.Itens.Count;
        var corte = DateTimeOffset.UtcNow.AddDays(-DiasParaItemRecente);
        var recentes = leitura.Itens.Count(i => i.Data is { } d && d >= corte);

        // Passo 3 — tem movimento recente?
        if (recentes < MinimoItensRecentes)
        {
            log.LogInformation(
                "Feed {Url} reprovado no passo 3: {Recentes} itens recentes de {Total}",
                url, recentes, total);
            return new FeedValidationResult(
                false, status, leitura.Formato, recentes, total, "sem_itens_recentes");
        }

        // Passo 4 — os itens vêm completos?
        var completos = leitura.Itens.Count(i =>
            !string.IsNullOrWhiteSpace(i.Titulo) &&
            !string.IsNullOrWhiteSpace(i.Link) &&
            i.Data is not null);

        if (total == 0 || (double)completos / total < FracaoMinimaCamposCompletos)
        {
            log.LogInformation(
                "Feed {Url} reprovado no passo 4: {Completos} de {Total} itens completos",
                url, completos, total);
            return new FeedValidationResult(
                false, status, leitura.Formato, recentes, total, "campos_incompletos");
        }

        return new FeedValidationResult(true, status, leitura.Formato, recentes, total, null);
    }

    /// <summary>
    /// GET com o timeout da configuração. Os redirects (até 3) e o User-Agent ficam no
    /// handler do <see cref="HttpClient"/> nomeado.
    /// </summary>
    private async Task<(string? Conteudo, int? Status, string? Falha)> BaixarAsync(
        string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return (null, null, "url_invalida");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.Coleta.TimeoutSegundos));

        try
        {
            using var resposta = await http.GetAsync(uri, timeout.Token);
            var status = (int)resposta.StatusCode;

            if (!resposta.IsSuccessStatusCode)
                return (null, status, $"http_{status}");

            var conteudo = await resposta.Content.ReadAsStringAsync(timeout.Token);
            return (conteudo, status, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (null, null, "timeout");
        }
        catch (HttpRequestException e)
        {
            log.LogDebug(e, "Falha de rede em {Url}", url);
            return (null, null, "timeout");
        }
    }
}
