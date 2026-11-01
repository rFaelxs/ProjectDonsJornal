using System.Net;
using FluentAssertions;
using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Jornal.Infrastructure.Feeds;
using Jornal.Integration.Tests.Apoio;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jornal.Integration.Tests;

/// <summary>
/// Critérios de aceite do validador (spec 02). Tudo contra um servidor HTTP fake, como a
/// própria spec pede — sem rede e sem depender de feed real.
/// </summary>
public class FeedValidatorTests
{
    private static FeedValidator CriarValidador()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("JornalPersonalizado/0.1 (teste)");

        var opcoes = Options.Create(new JornalOptions());
        return new FeedValidator(http, opcoes, NullLogger<FeedValidator>.Instance);
    }

    // CA-02.2 — reprova 404, HTML que não é feed, e feed sem item recente, cada um com o motivo certo.

    [Fact]
    public async Task CA_02_2_reprova_url_que_responde_404()
    {
        using var servidor = new ServidorHttpFake();
        var url = servidor.RotaComStatus("ausente", 404);

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeFalse();
        r.Motivo.Should().Be("http_404");
        r.HttpStatus.Should().Be(404);
    }

    [Fact]
    public async Task CA_02_2_reprova_html_que_nao_e_feed()
    {
        using var servidor = new ServidorHttpFake();
        var url = servidor.Rota("pagina", FeedsFake.Html(), "text/html");

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeFalse();
        r.Motivo.Should().Be("formato_invalido");
        r.Formato.Should().Be(FormatoFeed.Invalido);
    }

    [Fact]
    public async Task CA_02_2_reprova_feed_cujo_item_mais_recente_passa_de_7_dias()
    {
        using var servidor = new ServidorHttpFake();
        // 10 dias atrás: fora da janela de 7 dias do passo 3.
        var url = servidor.Rota("parado", FeedsFake.Rss(quantidade: 5, diasAtras: 10));

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeFalse();
        r.Motivo.Should().Be("sem_itens_recentes");
        r.ItensRecentes.Should().Be(0);
        r.TotalItens.Should().Be(5);
    }

    [Fact]
    public async Task Reprova_feed_com_menos_de_tres_itens_recentes()
    {
        using var servidor = new ServidorHttpFake();
        var url = servidor.Rota("magro", FeedsFake.Rss(quantidade: 2));

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeFalse("o passo 3 exige pelo menos 3 itens recentes");
        r.Motivo.Should().Be("sem_itens_recentes");
    }

    [Fact]
    public async Task Reprova_feed_com_menos_de_80_por_cento_dos_campos()
    {
        using var servidor = new ServidorHttpFake();
        // Itens recentes e com data, mas sem link: falha no passo 4.
        var url = servidor.Rota("incompleto", FeedsFake.Rss(quantidade: 5, comLink: false));

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeFalse();
        r.Motivo.Should().Be("campos_incompletos");
    }

    // CA-02.3 — aprova RSS 2.0 e Atom válidos, com itens recentes.

    [Fact]
    public async Task CA_02_3_aprova_rss_2_valido()
    {
        using var servidor = new ServidorHttpFake();
        var url = servidor.Rota("rss", FeedsFake.Rss(quantidade: 6));

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeTrue();
        r.Formato.Should().Be(FormatoFeed.Rss);
        r.ItensRecentes.Should().Be(6);
        r.Motivo.Should().BeNull();
    }

    [Fact]
    public async Task CA_02_3_aprova_atom_valido()
    {
        using var servidor = new ServidorHttpFake();
        var url = servidor.Rota("atom", FeedsFake.Atom(quantidade: 4), "application/atom+xml");

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeTrue();
        r.Formato.Should().Be(FormatoFeed.Atom);
        r.ItensRecentes.Should().Be(4);
    }

    [Fact]
    public async Task Aprova_feed_que_o_syndication_recusa_usando_o_leitor_tolerante()
    {
        using var servidor = new ServidorHttpFake();
        // Data "yyyy-MM-dd HH:mm:ss BRT" e entidades HTML: o Syndication desiste.
        // Este caso foi encontrado em feeds brasileiros reais durante o M1.
        var url = servidor.Rota("torto", FeedsFake.RssComDataTortaEEntidade(quantidade: 5));

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeTrue("o fallback com XDocument analisa a data com tolerância");
        r.Formato.Should().Be(FormatoFeed.Rss);
        r.ItensRecentes.Should().Be(5);
    }

    [Fact]
    public async Task Segue_redirect_dentro_do_limite()
    {
        using var servidor = new ServidorHttpFake();
        var destino = servidor.Rota("final", FeedsFake.Rss(quantidade: 5));
        var url = servidor.RotaRedirect("inicio", destino);

        var r = await CriarValidador().ValidateAsync(url, CancellationToken.None);

        r.Aprovado.Should().BeTrue();
    }

    [Fact]
    public async Task Reprova_url_que_nao_e_http()
    {
        var r = await CriarValidador().ValidateAsync(
            "ftp://exemplo.test/feed.xml", CancellationToken.None);

        r.Aprovado.Should().BeFalse();
        r.Motivo.Should().Be("url_invalida");
    }

    [Theory]
    [InlineData("Sun, 01 Nov 2026 10:00:00 GMT")]
    [InlineData("2026-11-01T10:00:00-03:00")]
    [InlineData("2026-11-01 10:00:00 BRT")]
    [InlineData("Sun, 01 Nov 2026 10:00:00 -0300")]
    // O TryParse valida o dia da semana contra a data: "Sun" porque 01/11/2026 é domingo.
    public void Leitor_analisa_os_formatos_de_data_que_aparecem_na_pratica(string texto)
    {
        LeitorSindicacao.DataTolerante(texto).Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ontem")]
    [InlineData(null)]
    public void Leitor_devolve_nulo_para_data_ilegivel(string? texto)
    {
        LeitorSindicacao.DataTolerante(texto).Should().BeNull();
    }
}
