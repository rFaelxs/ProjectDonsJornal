using System.Globalization;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Jornal.Core.Catalogo;

namespace Jornal.Infrastructure.Feeds;

/// <summary>Um item cru, como saiu do XML, antes de qualquer normalização.</summary>
public record ItemCru(string? Titulo, string? Link, DateTimeOffset? Data, string? Resumo);

/// <summary>O que a leitura de um XML de feed produziu.</summary>
public record LeituraFeed(FormatoFeed Formato, IReadOnlyList<ItemCru> Itens, bool UsouFallback);

/// <summary>
/// Lê RSS 2.0 e Atom 1.0. Tenta primeiro o <c>System.ServiceModel.Syndication</c>, indicado no
/// levantamento. Quando ele recusa o XML — quase sempre por data fora do RFC 822, comum nos
/// feeds brasileiros (risco listado na spec 09) — cai para uma leitura própria com XDocument,
/// que analisa as datas com tolerância em vez de lançar exceção.
/// </summary>
public static class LeitorSindicacao
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace ConteudoCodificado = "http://purl.org/rss/1.0/modules/content/";

    /// <summary>Devolve null quando o conteúdo não é um feed reconhecível.</summary>
    public static LeituraFeed? Ler(string conteudo)
    {
        var formato = DetectarFormato(conteudo);
        if (formato is null) return null;

        var viaSyndication = TentarSyndication(conteudo);
        if (viaSyndication is not null)
            return new LeituraFeed(formato.Value, viaSyndication, UsouFallback: false);

        var viaFallback = TentarXDocument(conteudo, formato.Value);
        return viaFallback is null
            ? null
            : new LeituraFeed(formato.Value, viaFallback, UsouFallback: true);
    }

    /// <summary>
    /// Remove o BOM e troca entidades HTML que não existem em XML. O passo 2 da spec 02
    /// permite uma tentativa de sanitização.
    /// </summary>
    public static string Sanitizar(string conteudo)
    {
        var limpo = conteudo.TrimStart('﻿', '​').Trim();

        // &nbsp; e companhia são válidos em HTML, mas não em XML sem DTD.
        foreach (var (entidade, substituto) in Entidades)
            limpo = limpo.Replace(entidade, substituto);

        // Um & solto quebra o parser. Só escapa os que não iniciam entidade válida.
        limpo = Regex.Replace(limpo, @"&(?!(?:amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);)", "&amp;");

        // Lixo antes da declaração XML ou do primeiro elemento.
        var inicio = limpo.IndexOf("<?xml", StringComparison.Ordinal);
        if (inicio < 0) inicio = limpo.IndexOf('<');
        return inicio > 0 ? limpo[inicio..] : limpo;
    }

    private static readonly (string Entidade, string Substituto)[] Entidades =
    [
        ("&nbsp;", " "), ("&ndash;", "–"), ("&mdash;", "—"), ("&rsquo;", "’"),
        ("&lsquo;", "‘"), ("&ldquo;", "“"), ("&rdquo;", "”"), ("&hellip;", "…"),
        ("&aacute;", "á"), ("&eacute;", "é"), ("&iacute;", "í"), ("&oacute;", "ó"),
        ("&uacute;", "ú"), ("&atilde;", "ã"), ("&otilde;", "õ"), ("&ccedil;", "ç"),
        ("&acirc;", "â"), ("&ecirc;", "ê"), ("&ocirc;", "ô"), ("&agrave;", "à")
    ];

    private static FormatoFeed? DetectarFormato(string conteudo)
    {
        try
        {
            // Lê só até o elemento raiz: basta o nome para classificar.
            using var leitor = XmlReader.Create(new StringReader(conteudo),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, CloseInput = true });

            while (leitor.Read())
            {
                if (leitor.NodeType != XmlNodeType.Element) continue;
                return leitor.LocalName switch
                {
                    "rss" or "RDF" => FormatoFeed.Rss,
                    "feed" => FormatoFeed.Atom,
                    _ => null
                };
            }
        }
        catch (XmlException)
        {
            // XML quebrado: quem chama tenta sanitizar e repetir.
        }

        return null;
    }

    private static List<ItemCru>? TentarSyndication(string conteudo)
    {
        try
        {
            using var leitor = XmlReader.Create(new StringReader(conteudo),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, CloseInput = true });

            var feed = SyndicationFeed.Load(leitor);
            if (feed is null) return null;

            return [.. feed.Items.Select(i => new ItemCru(
                Titulo: i.Title?.Text,
                Link: i.Links.FirstOrDefault()?.Uri?.ToString() ?? i.Id,
                Data: EscolherData(i.PublishDate, i.LastUpdatedTime),
                Resumo: i.Summary?.Text ?? TextoDoConteudo(i)))];
        }
        // Catch largo de propósito: este método é a tentativa otimista. Qualquer recusa do
        // Syndication — XML inválido, data fora do RFC 822, `<rss>` sem atributo version —
        // significa apenas "use o leitor tolerante", nunca "falhe o feed".
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;
        }
    }

    private static List<ItemCru>? TentarXDocument(string conteudo, FormatoFeed formato)
    {
        try
        {
            var doc = XDocument.Parse(conteudo, LoadOptions.None);
            if (doc.Root is null) return null;

            return formato == FormatoFeed.Atom
                ? [.. doc.Root.Elements(Atom + "entry").Select(LerEntradaAtom)]
                : [.. doc.Descendants("item").Select(LerItemRss)];
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static ItemCru LerItemRss(XElement item) => new(
        Titulo: item.Element("title")?.Value,
        Link: item.Element("link")?.Value ?? item.Element("guid")?.Value,
        Data: DataTolerante(item.Element("pubDate")?.Value ?? item.Element("date")?.Value),
        Resumo: item.Element("description")?.Value
                ?? item.Element(ConteudoCodificado + "encoded")?.Value);

    private static ItemCru LerEntradaAtom(XElement entrada) => new(
        Titulo: entrada.Element(Atom + "title")?.Value,
        Link: entrada.Elements(Atom + "link")
                  .FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")
                  ?.Attribute("href")?.Value
              ?? entrada.Element(Atom + "id")?.Value,
        Data: DataTolerante(entrada.Element(Atom + "published")?.Value
                            ?? entrada.Element(Atom + "updated")?.Value),
        Resumo: entrada.Element(Atom + "summary")?.Value
                ?? entrada.Element(Atom + "content")?.Value);

    private static string? TextoDoConteudo(SyndicationItem item) =>
        item.Content is TextSyndicationContent texto ? texto.Text : null;

    private static DateTimeOffset? EscolherData(DateTimeOffset publicado, DateTimeOffset atualizado)
    {
        if (publicado > DateTimeOffset.MinValue) return publicado;
        if (atualizado > DateTimeOffset.MinValue) return atualizado;
        return null;
    }

    /// <summary>
    /// Aceita RFC 822, ISO 8601 e as variações que aparecem na prática, incluindo fuso
    /// escrito como nome ("BRT") em vez de deslocamento.
    /// </summary>
    public static DateTimeOffset? DataTolerante(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpo = texto.Trim();

        if (DateTimeOffset.TryParse(limpo, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var direto))
            return direto;

        // Troca nomes de fuso por deslocamento e tenta de novo.
        foreach (var (nome, deslocamento) in Fusos)
        {
            if (!limpo.EndsWith(nome, StringComparison.OrdinalIgnoreCase)) continue;
            var comDeslocamento = limpo[..^nome.Length].TrimEnd() + deslocamento;
            if (DateTimeOffset.TryParse(comDeslocamento, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var ajustado))
                return ajustado;
        }

        return null;
    }

    private static readonly (string Nome, string Deslocamento)[] Fusos =
    [
        ("BRT", "-0300"), ("BRST", "-0200"), ("GMT", "+0000"), ("UTC", "+0000"),
        ("EST", "-0500"), ("EDT", "-0400"), ("PST", "-0800"), ("PDT", "-0700")
    ];
}
