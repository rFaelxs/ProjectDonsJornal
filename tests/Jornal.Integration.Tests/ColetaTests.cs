using FluentAssertions;
using Jornal.Core.Catalogo;
using Jornal.Core.Coleta;
using Jornal.Core.Comum;
using Jornal.Infrastructure.Coleta;
using Jornal.Infrastructure.Feeds;
using Jornal.Infrastructure.Persistencia;
using Jornal.Integration.Tests.Apoio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jornal.Integration.Tests;

/// <summary>Critérios de aceite da coleta e da indexação (spec 04).</summary>
[Collection(BancoCollection.Nome)]
public class ColetaTests(BancoFixture banco)
{
    private static readonly IOptions<JornalOptions> Opcoes = Options.Create(new JornalOptions());

    private LeitorDeFeed CriarLeitor()
    {
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        return new LeitorDeFeed(http, Opcoes, NullLogger<LeitorDeFeed>.Instance);
    }

    private ColetaService CriarColeta(JornalDbContext db, IFeedReader leitor) =>
        new(db, leitor, Opcoes, NullLogger<ColetaService>.Instance);

    private Indexador CriarIndexador(JornalDbContext db, GeradorEmbeddingFake gerador) =>
        new(db, gerador, Opcoes, NullLogger<Indexador>.Instance);

    /// <summary>Cria uma categoria com os feeds apontando para as URLs dadas, todos ativos.</summary>
    private async Task SemearFeedsAsync(string slug, params string[] urls)
    {
        await using var db = banco.CriarContexto();

        var categoria = new Categoria
        {
            Slug = slug,
            Nome = $"Categoria {slug}",
            Descricao = "Categoria dos testes de coleta."
        };

        for (var i = 0; i < urls.Length; i++)
            categoria.Feeds.Add(new Feed
            {
                NomeVeiculo = $"Veículo {i + 1}",
                Url = urls[i],
                Ativo = true
            });

        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
    }

    // CA-04.1 — coletar duas vezes o mesmo feed não cria itens novos.

    [Fact]
    public async Task CA_04_1_coletar_duas_vezes_o_mesmo_feed_nao_cria_itens_novos()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();
        var url = servidor.Rota("feed", FeedsFake.Rss(quantidade: 5));
        await SemearFeedsAsync("repetida", url);

        await using (var db = banco.CriarContexto())
        {
            var primeira = await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);
            primeira.ItensNovos.Should().Be(5);
        }

        await using (var db = banco.CriarContexto())
        {
            var segunda = await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);
            segunda.ItensNovos.Should().Be(0, "o link_hash já existe");
            segunda.ItensIgnoradosPorLink.Should().Be(5);
        }

        await using var conferencia = banco.CriarContexto();
        (await conferencia.Itens.CountAsync()).Should().Be(5);
    }

    // CA-04.3 — com 3 feeds e 1 devolvendo 500, grava os itens dos outros 2 e 1 coleta_falha.

    [Fact]
    public async Task CA_04_3_um_feed_com_erro_nao_impede_os_outros()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();

        var bom1 = servidor.Rota("bom1", FeedsFake.Rss(quantidade: 4));
        var bom2 = servidor.Rota("bom2", FeedsFake.Atom(quantidade: 3), "application/atom+xml");
        var ruim = servidor.RotaComStatus("ruim", 500);

        await SemearFeedsAsync("resiliente", bom1, bom2, ruim);

        await using var db = banco.CriarContexto();
        var resumo = await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);

        resumo.FeedsLidos.Should().Be(3);
        resumo.FeedsComFalha.Should().Be(1);
        resumo.ItensNovos.Should().Be(7, "4 do RSS mais 3 do Atom");
        resumo.TodosFalharam.Should().BeFalse();

        await using var conferencia = banco.CriarContexto();
        var falhas = await conferencia.ColetaFalhas.AsNoTracking().ToListAsync();
        falhas.Should().ContainSingle();
        falhas[0].Tipo.Should().Be(TipoColetaFalha.Http);
        falhas[0].Mensagem.Should().Contain("500");
    }

    [Fact]
    public async Task Tres_falhas_consecutivas_desativam_o_feed()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();
        await SemearFeedsAsync("desistente", servidor.RotaComStatus("sempre-ruim", 500));

        for (var tentativa = 1; tentativa <= 3; tentativa++)
        {
            await using var db = banco.CriarContexto();
            await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);
        }

        await using var conferencia = banco.CriarContexto();
        var feed = await conferencia.Feeds.AsNoTracking().FirstAsync();

        feed.FalhasConsecutivas.Should().BeGreaterThanOrEqualTo(3);
        feed.Ativo.Should().BeFalse("3 falhas consecutivas desativam o feed (spec 02)");
    }

    [Fact]
    public async Task Todos_os_feeds_falhando_e_sinalizado_no_resumo()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();
        await SemearFeedsAsync("apagao",
            servidor.RotaComStatus("x", 500), servidor.RotaComStatus("y", 503));

        await using var db = banco.CriarContexto();
        var resumo = await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);

        resumo.TodosFalharam.Should().BeTrue("é o único caso em que o ciclo aborta (RF17)");
    }

    [Fact]
    public async Task CA_04_4_nenhum_resumo_gravado_passa_de_600_caracteres_nem_tem_html()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();
        await SemearFeedsAsync("html", servidor.Rota("sujo", FeedsFake.Rss(quantidade: 5)));

        await using var db = banco.CriarContexto();
        await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);

        await using var conferencia = banco.CriarContexto();
        var itens = await conferencia.Itens.AsNoTracking().ToListAsync();

        itens.Should().NotBeEmpty();
        itens.Should().OnlyContain(i => i.Resumo.Length <= 600);
        itens.Should().OnlyContain(i => !i.Resumo.Contains('<') && !i.Resumo.Contains('>'));
    }

    [Fact]
    public async Task Feed_que_responde_304_nao_gera_item_nem_falha()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();
        await SemearFeedsAsync("cacheado", servidor.RotaComStatus("naomudou", 304));

        await using var db = banco.CriarContexto();
        var resumo = await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);

        resumo.FeedsComFalha.Should().Be(0, "304 é resposta de sucesso, não falha");
        resumo.ItensNovos.Should().Be(0);

        await using var conferencia = banco.CriarContexto();
        (await conferencia.ColetaFalhas.CountAsync()).Should().Be(0);
    }

    // CA-04.2 — dois itens com links diferentes e similaridade ≥ 0.93: o segundo fica
    // com duplicata_de e 0 trechos.

    [Fact]
    public async Task CA_04_2_item_muito_similar_vira_duplicata_e_nao_gera_trechos()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();

        // Dois feeds com o MESMO texto em links diferentes. O gerador fake é
        // determinístico pelo texto, então os embeddings saem idênticos (similaridade 1.0).
        const string textoRepetido = "Banco Central mantém a Selic em 10,5% ao ano.";
        var feedA = servidor.Rota("a", RssComUmItem("https://a.test/noticia", textoRepetido));
        var feedB = servidor.Rota("b", RssComUmItem("https://b.test/noticia", textoRepetido));

        await SemearFeedsAsync("duplicada", feedA, feedB);

        await using (var db = banco.CriarContexto())
        {
            await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);
        }

        await using (var db = banco.CriarContexto())
        {
            await CriarIndexador(db, new GeradorEmbeddingFake())
                .IndexarPendentesAsync(CancellationToken.None);
        }

        await using var conferencia = banco.CriarContexto();
        var itens = await conferencia.Itens.AsNoTracking()
            .Include(i => i.Trechos).OrderBy(i => i.Link).ToListAsync();

        itens.Should().HaveCount(2, "links diferentes, então os dois são gravados (RF16)");

        var duplicata = itens.Single(i => i.DuplicataDe != null);
        var original = itens.Single(i => i.DuplicataDe == null);

        duplicata.DuplicataDe.Should().Be(original.Id);
        duplicata.Trechos.Should().BeEmpty("item duplicado não gera trecho");
        original.Trechos.Should().HaveCount(1);
    }

    [Fact]
    public async Task Itens_diferentes_nao_sao_marcados_como_duplicata()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();

        var feedA = servidor.Rota("dif-a",
            RssComUmItem("https://a.test/1", "Selic mantida em 10,5% ao ano pelo Copom."));
        var feedB = servidor.Rota("dif-b",
            RssComUmItem("https://b.test/1", "Flamengo vence o clássico por 2 a 0 no Maracanã."));

        await SemearFeedsAsync("distintos", feedA, feedB);

        await using (var db = banco.CriarContexto())
            await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);

        await using (var db = banco.CriarContexto())
            await CriarIndexador(db, new GeradorEmbeddingFake())
                .IndexarPendentesAsync(CancellationToken.None);

        await using var conferencia = banco.CriarContexto();
        var itens = await conferencia.Itens.AsNoTracking().Include(i => i.Trechos).ToListAsync();

        itens.Should().OnlyContain(i => i.DuplicataDe == null);
        itens.Should().OnlyContain(i => i.Trechos.Count == 1);
    }

    [Fact]
    public async Task Indexar_duas_vezes_nao_duplica_trechos()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();
        await SemearFeedsAsync("idempotente", servidor.Rota("feed", FeedsFake.Rss(quantidade: 4)));

        await using (var db = banco.CriarContexto())
            await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);

        await using (var db = banco.CriarContexto())
            await CriarIndexador(db, new GeradorEmbeddingFake())
                .IndexarPendentesAsync(CancellationToken.None);

        int segundaPassada;
        await using (var db = banco.CriarContexto())
            segundaPassada = await CriarIndexador(db, new GeradorEmbeddingFake())
                .IndexarPendentesAsync(CancellationToken.None);

        segundaPassada.Should().Be(0, "a indexação processa só itens sem trechos");

        await using var conferencia = banco.CriarContexto();
        (await conferencia.Trechos.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task Trecho_guarda_os_metadados_desnormalizados_do_item()
    {
        await banco.LimparCatalogoAsync();
        using var servidor = new ServidorHttpFake();
        await SemearFeedsAsync("metadados", servidor.Rota("feed", FeedsFake.Rss(quantidade: 2)));

        await using (var db = banco.CriarContexto())
            await CriarColeta(db, CriarLeitor()).ColetarAsync(CancellationToken.None);

        await using (var db = banco.CriarContexto())
            await CriarIndexador(db, new GeradorEmbeddingFake())
                .IndexarPendentesAsync(CancellationToken.None);

        await using var conferencia = banco.CriarContexto();
        var trecho = await conferencia.Trechos.AsNoTracking()
            .Include(t => t.Item).FirstAsync();

        // RF14: fonte, publicado_em e categoria_id ficam no trecho para filtrar sem join.
        trecho.Fonte.Should().Be(trecho.Item!.Fonte);
        trecho.CategoriaId.Should().Be(trecho.Item.CategoriaId);
        trecho.PublicadoEm.Should().BeCloseTo(trecho.Item.PublicadoEm, TimeSpan.FromSeconds(1));
        trecho.Embedding.Should().NotBeNull().And.HaveCount(1024);
    }

    /// <summary>RSS com um item só, para controlar o texto que vira embedding.</summary>
    private static string RssComUmItem(string link, string texto) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
          <channel>
            <title>Feed de um item</title>
            <link>https://exemplo.test</link>
            <description>Teste</description>
            <item>
              <title>{texto}</title>
              <link>{link}</link>
              <description></description>
              <pubDate>{DateTimeOffset.UtcNow:r}</pubDate>
            </item>
          </channel>
        </rss>
        """;
}
