using System.Net;
using Jornal.Core.Catalogo;
using Jornal.Core.Coleta;
using Jornal.Core.Comum;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jornal.Infrastructure.Feeds;

/// <summary>
/// Lê um feed e devolve os itens já normalizados (spec 04). Usa <c>If-Modified-Since</c> e
/// <c>ETag</c> quando o feed oferece, para não baixar o mesmo XML a cada ciclo.
/// Falha não lança: vira <see cref="FeedReadResult.Falha"/>, porque a coleta precisa
/// continuar com os outros feeds (RF17).
/// </summary>
public class LeitorDeFeed(
    HttpClient http,
    IOptions<JornalOptions> options,
    ILogger<LeitorDeFeed> log) : IFeedReader
{
    public async Task<FeedReadResult> ReadAsync(Feed feed, CancellationToken ct)
    {
        var opcoes = options.Value.Coleta;

        if (!Uri.TryCreate(feed.Url, UriKind.Absolute, out var uri))
            return Falha(feed, TipoColetaFalha.Outro, $"URL inválida: {feed.Url}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(opcoes.TimeoutSegundos));

        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, uri);

            // Cabeçalhos de cache: economizam banda e deixam o feed avisar que nada mudou.
            if (feed.ETag is { Length: > 0 } etag)
                pedido.Headers.TryAddWithoutValidation("If-None-Match", etag);
            if (feed.UltimoModificadoEm is { } modificado)
                pedido.Headers.IfModifiedSince = modificado;

            using var resposta = await http.SendAsync(pedido, timeout.Token);
            var status = (int)resposta.StatusCode;

            if (resposta.StatusCode == HttpStatusCode.NotModified)
            {
                log.LogDebug("Feed {Veiculo} respondeu 304: nada novo", feed.NomeVeiculo);
                return new FeedReadResult(feed.Id, [], status, feed.ETag,
                    feed.UltimoModificadoEm, NaoModificado: true, null);
            }

            if (!resposta.IsSuccessStatusCode)
                return Falha(feed, TipoColetaFalha.Http, $"HTTP {status}", status);

            var conteudo = await resposta.Content.ReadAsStringAsync(timeout.Token);

            var leitura = LeitorSindicacao.Ler(conteudo)
                          ?? LeitorSindicacao.Ler(LeitorSindicacao.Sanitizar(conteudo));

            if (leitura is null)
                return Falha(feed, TipoColetaFalha.Parse,
                    "Não foi possível ler como RSS 2.0 nem Atom 1.0", status);

            var agora = DateTimeOffset.UtcNow;
            var itens = leitura.Itens
                .Select(cru => Normalizar(cru, agora))
                .Where(i => i is not null)
                .Select(i => i!)
                .ToList();

            return new FeedReadResult(
                feed.Id, itens, status,
                resposta.Headers.ETag?.ToString(),
                resposta.Content.Headers.LastModified,
                NaoModificado: false, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Falha(feed, TipoColetaFalha.Timeout,
                $"Não respondeu em {opcoes.TimeoutSegundos}s");
        }
        catch (HttpRequestException e)
        {
            return Falha(feed, TipoColetaFalha.Http, e.Message);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Falha(feed, TipoColetaFalha.Outro, e.Message);
        }
    }

    /// <summary>
    /// Aplica a tabela de normalização da spec 04. Devolve null quando o item não tem o
    /// mínimo para existir: sem título ou sem link não há matéria nem rastreabilidade.
    /// </summary>
    private static ItemLido? Normalizar(ItemCru cru, DateTimeOffset agora)
    {
        var titulo = NormalizadorItem.TextoPuro(cru.Titulo);
        if (titulo.Length == 0 || string.IsNullOrWhiteSpace(cru.Link)) return null;

        var link = cru.Link.Trim();
        if (!Uri.TryCreate(link, UriKind.Absolute, out _)) return null;

        // Item sem data no feed recebe a hora da coleta e o flag (spec 04).
        var dataEstimada = cru.Data is null;
        var publicadoEm = cru.Data ?? agora;

        // Data no futuro é erro do feed; tratada como estimada para não furar a janela de 24h.
        if (publicadoEm > agora.AddHours(1))
        {
            publicadoEm = agora;
            dataEstimada = true;
        }

        return new ItemLido(
            Titulo: titulo.Length > 500 ? titulo[..500] : titulo,
            Resumo: NormalizadorItem.Resumo(cru.Resumo),
            Link: NormalizadorItem.CanonicalizarLink(link),
            LinkHash: NormalizadorItem.HashDoLink(link),
            PublicadoEm: publicadoEm,
            DataEstimada: dataEstimada);
    }

    private static FeedReadResult Falha(
        Feed feed, TipoColetaFalha tipo, string mensagem, int? status = null) =>
        new(feed.Id, [], status, feed.ETag, feed.UltimoModificadoEm, false, (tipo, mensagem));
}
