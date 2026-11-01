using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Jornal.Core.Coleta;

/// <summary>
/// Normalização de um item lido do feed (spec 04). São funções puras, sem I/O, para que
/// RN02 e a canonicalização de link sejam testáveis sem banco nem rede.
/// </summary>
public static partial class NormalizadorItem
{
    /// <summary>
    /// Limite do resumo (RN02). A spec é explícita: guardamos título e resumo, nunca a
    /// matéria inteira.
    /// </summary>
    public const int LimiteResumo = 600;

    /// <summary>Parâmetros de rastreamento que não identificam o conteúdo.</summary>
    private static readonly string[] ParametrosDescartaveis =
    [
        "fbclid", "gclid", "msclkid", "igshid", "mc_cid", "mc_eid",
        "ref", "referrer", "spm", "cmpid", "xtor"
    ];

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex Tags();

    /// <summary>
    /// <c>script</c>, <c>style</c> e <c>noscript</c> precisam sair com o conteúdo. Remover
    /// só as tags deixaria o corpo do script virar texto do resumo.
    /// </summary>
    [GeneratedRegex(@"<(script|style|noscript)\b[^>]*>.*?</\1\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BlocosNaoTextuais();

    /// <summary>Comentários HTML, que às vezes carregam marcação de anúncio.</summary>
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex Comentarios();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex EspacosRepetidos();

    /// <summary>
    /// Remove tags, decodifica entidades e colapsa espaços. Usado no título e no resumo
    /// (CA-04.4 exige resumo sem HTML).
    /// </summary>
    public static string TextoPuro(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return string.Empty;

        var limpo = Comentarios().Replace(bruto, " ");
        limpo = BlocosNaoTextuais().Replace(limpo, " ");
        var decodificado = WebUtility.HtmlDecode(Tags().Replace(limpo, " "));

        // Pode haver entidade dentro de entidade (&amp;nbsp;). Uma segunda passada resolve
        // o caso comum sem abrir brecha para laço infinito.
        if (decodificado.Contains('&')) decodificado = WebUtility.HtmlDecode(decodificado);

        // Se a decodificação revelou marcação, limpa de novo — pela mesma ordem.
        if (decodificado.Contains('<'))
        {
            decodificado = BlocosNaoTextuais().Replace(decodificado, " ");
            decodificado = Tags().Replace(decodificado, " ");
        }

        return EspacosRepetidos().Replace(decodificado, " ").Trim();
    }

    /// <summary>
    /// Texto puro cortado em <see cref="LimiteResumo"/> caracteres, preferindo o fim de
    /// frase mais próximo (spec 04, RN02).
    /// </summary>
    public static string Resumo(string? bruto)
    {
        var texto = TextoPuro(bruto);
        if (texto.Length <= LimiteResumo) return texto;

        var janela = texto[..LimiteResumo];

        // Último fim de frase na janela. Só vale se não cortar o resumo pela metade —
        // abaixo de 60% do limite, um corte por palavra preserva mais informação.
        var fimDeFrase = janela.LastIndexOfAny(['.', '!', '?']);
        if (fimDeFrase >= LimiteResumo * 0.6)
            return janela[..(fimDeFrase + 1)].TrimEnd();

        var ultimoEspaco = janela.LastIndexOf(' ');
        var cortado = ultimoEspaco > 0 ? janela[..ultimoEspaco] : janela;
        return cortado.TrimEnd(' ', ',', ';', ':', '-') + "…";
    }

    /// <summary>
    /// Canonicaliza o link: minúsculas no host, sem <c>utm_*</c>, sem os parâmetros de
    /// rastreamento conhecidos e sem fragmento (spec 04, CA-04.5).
    /// </summary>
    public static string CanonicalizarLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link)) return string.Empty;

        var limpo = link.Trim();

        if (!Uri.TryCreate(limpo, UriKind.Absolute, out var uri))
            return limpo;

        var construtor = new UriBuilder(uri)
        {
            // O host não distingue maiúsculas; o caminho distingue, então fica como está.
            Host = uri.Host.ToLowerInvariant(),

            // O fragmento é navegação dentro da página, nunca identidade do conteúdo.
            Fragment = string.Empty
        };

        // Porta padrão explícita viraria link diferente para o mesmo recurso.
        if ((uri.Scheme == Uri.UriSchemeHttp && uri.Port == 80) ||
            (uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443))
            construtor.Port = -1;

        construtor.Query = FiltrarQuery(uri.Query);

        // Barra final sozinha não muda o recurso.
        var resultado = construtor.Uri.ToString();
        if (construtor.Query.Length == 0 && resultado.EndsWith('/') && uri.AbsolutePath != "/")
            resultado = resultado[..^1];

        return resultado;
    }

    private static string FiltrarQuery(string query)
    {
        if (string.IsNullOrEmpty(query) || query == "?") return string.Empty;

        var mantidos = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(par =>
            {
                var chave = par.Split('=', 2)[0];
                return !chave.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
                       && !ParametrosDescartaveis.Contains(chave, StringComparer.OrdinalIgnoreCase);
            })
            // Ordena para que a mesma URL com parâmetros em ordem diferente gere um hash só.
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return mantidos.Count == 0 ? string.Empty : string.Join('&', mantidos);
    }

    /// <summary>
    /// SHA-256 do link canônico, em hexadecimal minúsculo. É a chave única de
    /// deduplicação por link (RF15, etapa 1).
    /// </summary>
    public static string HashDoLink(string link) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizarLink(link)))).ToLowerInvariant();
}
