using System.Globalization;

namespace Jornal.Integration.Tests.Apoio;

/// <summary>Monta XML de feed para os testes, com controle sobre datas e campos faltantes.</summary>
public static class FeedsFake
{
    /// <summary>RSS 2.0 válido, com <paramref name="quantidade"/> itens publicados hoje.</summary>
    public static string Rss(int quantidade = 5, int diasAtras = 0, bool comData = true,
                            bool comTitulo = true, bool comLink = true)
    {
        var itens = string.Concat(Enumerable.Range(1, quantidade).Select(i =>
        {
            var data = DateTimeOffset.UtcNow.AddDays(-diasAtras).AddMinutes(-i);
            return $"""
                  <item>
                    {(comTitulo ? $"<title>Notícia de teste {i}</title>" : "")}
                    {(comLink ? $"<link>https://exemplo.test/noticia-{i}</link>" : "")}
                    <description>Resumo da notícia de teste número {i}.</description>
                    {(comData ? $"<pubDate>{Rfc822(data)}</pubDate>" : "")}
                  </item>
                """;
        }));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>Feed de teste</title>
                <link>https://exemplo.test</link>
                <description>Feed usado pelos testes.</description>
            {itens}
              </channel>
            </rss>
            """;
    }

    /// <summary>Atom 1.0 válido.</summary>
    public static string Atom(int quantidade = 5, int diasAtras = 0)
    {
        var entradas = string.Concat(Enumerable.Range(1, quantidade).Select(i =>
        {
            var data = DateTimeOffset.UtcNow.AddDays(-diasAtras).AddMinutes(-i);
            return $"""
                  <entry>
                    <title>Entrada de teste {i}</title>
                    <link rel="alternate" href="https://exemplo.test/entrada-{i}"/>
                    <id>urn:uuid:entrada-{i}</id>
                    <summary>Resumo da entrada {i}.</summary>
                    <published>{data:yyyy-MM-ddTHH:mm:sszzz}</published>
                  </entry>
                """;
        }));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>Feed Atom de teste</title>
              <id>urn:uuid:feed-de-teste</id>
              <updated>{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:sszzz}</updated>
            {entradas}
            </feed>
            """;
    }

    /// <summary>Uma página HTML — o caso "a URL responde, mas não é feed" do CA-02.2.</summary>
    public static string Html() => """
        <!DOCTYPE html>
        <html lang="pt-BR">
          <head><title>Não sou um feed</title></head>
          <body><h1>Página comum</h1><p>Isto é HTML, não RSS.</p></body>
        </html>
        """;

    /// <summary>
    /// RSS com data fora do RFC 822 e entidade HTML solta — o tipo de XML que faz o
    /// System.ServiceModel.Syndication desistir e exige o leitor tolerante.
    /// </summary>
    public static string RssComDataTortaEEntidade(int quantidade = 5)
    {
        var itens = string.Concat(Enumerable.Range(1, quantidade).Select(i =>
        {
            var data = DateTime.UtcNow.AddMinutes(-i);
            return $"""
                  <item>
                    <title>Not&iacute;cia {i} &ndash; com entidade</title>
                    <link>https://exemplo.test/torto-{i}</link>
                    <description>Resumo com &nbsp; entidade e &amp; solto.</description>
                    <pubDate>{data:yyyy-MM-dd HH:mm:ss} BRT</pubDate>
                  </item>
                """;
        }));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
              <channel>
                <title>Feed torto</title>
                <link>https://exemplo.test</link>
                <description>Datas fora do RFC 822.</description>
            {itens}
              </channel>
            </rss>
            """;
    }

    /// <summary>Formato "r" (RFC 1123, em GMT) — é o que o pubDate de RSS 2.0 espera.</summary>
    private static string Rfc822(DateTimeOffset data) =>
        data.ToString("r", CultureInfo.InvariantCulture);
}
