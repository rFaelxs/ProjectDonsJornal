using FluentAssertions;
using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Jornal.Infrastructure.Feeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jornal.Integration.Tests;

/// <summary>
/// Orquestração do <c>validar-feeds</c> e a verificação de RN08 que a spec 02 exige no fim
/// do comando. O validador em si é fake aqui — o que está sob teste é o efeito no banco.
/// </summary>
[Collection(BancoCollection.Nome)]
public class ValidacaoFeedsTests(BancoFixture banco)
{
    /// <summary>Aprova ou reprova conforme a URL, para o teste controlar o resultado.</summary>
    private class ValidadorFake(Func<string, bool> aprovar) : IFeedValidator
    {
        public int Chamadas { get; private set; }

        public Task<FeedValidationResult> ValidateAsync(string url, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(aprovar(url)
                ? new FeedValidationResult(true, 200, FormatoFeed.Rss, 5, 5, null)
                : FeedValidationResult.Reprovado("sem_itens_recentes", 200, FormatoFeed.Rss));
        }
    }

    private async Task<Guid> SemearCategoriaAsync(string slug, params string[] urls)
    {
        await using var db = banco.CriarContexto();

        var categoria = new Categoria
        {
            Slug = slug,
            Nome = $"Categoria {slug}",
            Descricao = "Categoria dos testes de validação."
        };

        foreach (var url in urls)
            categoria.Feeds.Add(new Feed
            {
                NomeVeiculo = $"Veículo {url[^6..]}",
                Url = url,
                Ativo = false
            });

        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        return categoria.Id;
    }

    private ValidacaoFeedsService Criar(JornalDbContextWrapper wrapper, IFeedValidator validador) =>
        new(wrapper.Db, validador, Options.Create(new JornalOptions()),
            NullLogger<ValidacaoFeedsService>.Instance);

    /// <summary>Só para carregar o contexto e descartá-lo junto com o teste.</summary>
    private sealed class JornalDbContextWrapper(BancoFixture banco) : IAsyncDisposable
    {
        public Infrastructure.Persistencia.JornalDbContext Db { get; } = banco.CriarContexto();
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task Sem_atualizar_o_comando_nao_mexe_no_campo_ativo()
    {
        await banco.LimparCatalogoAsync();
        await SemearCategoriaAsync("so-relato", "https://a.test/1.xml", "https://a.test/2.xml");

        await using var wrapper = new JornalDbContextWrapper(banco);
        var servico = Criar(wrapper, new ValidadorFake(_ => true));

        var relatorio = await servico.ValidarAsync(null, atualizar: false, CancellationToken.None);

        relatorio.Aprovados.Should().Be(2);
        relatorio.Feeds.Should().OnlyContain(f => !f.AtivoAlterado);

        await using var conferencia = banco.CriarContexto();
        (await conferencia.Feeds.CountAsync(f => f.Ativo)).Should().Be(0);
    }

    [Fact]
    public async Task Com_atualizar_o_comando_liga_os_aprovados_e_desliga_os_reprovados()
    {
        await banco.LimparCatalogoAsync();
        await SemearCategoriaAsync("misto", "https://ok.test/bom.xml", "https://no.test/ruim.xml");

        await using var wrapper = new JornalDbContextWrapper(banco);
        var servico = Criar(wrapper, new ValidadorFake(url => url.Contains("ok.test")));

        var relatorio = await servico.ValidarAsync(null, atualizar: true, CancellationToken.None);

        relatorio.Aprovados.Should().Be(1);
        relatorio.Reprovados.Should().Be(1);

        await using var conferencia = banco.CriarContexto();
        var feeds = await conferencia.Feeds.AsNoTracking().ToListAsync();

        feeds.Single(f => f.Url.Contains("bom")).Ativo.Should().BeTrue();
        feeds.Single(f => f.Url.Contains("ruim")).Ativo.Should().BeFalse();
        feeds.Should().OnlyContain(f => f.ValidadoEm != null);
    }

    [Fact]
    public async Task Cada_execucao_grava_uma_linha_de_historico_por_feed()
    {
        await banco.LimparCatalogoAsync();
        await SemearCategoriaAsync("historico", "https://h.test/1.xml");

        await using var primeira = new JornalDbContextWrapper(banco);
        await Criar(primeira, new ValidadorFake(_ => true))
            .ValidarAsync(null, atualizar: true, CancellationToken.None);

        await using var segunda = new JornalDbContextWrapper(banco);
        await Criar(segunda, new ValidadorFake(_ => false))
            .ValidarAsync(null, atualizar: true, CancellationToken.None);

        await using var conferencia = banco.CriarContexto();
        var validacoes = await conferencia.FeedValidacoes
            .AsNoTracking().OrderBy(v => v.ExecutadoEm).ToListAsync();

        validacoes.Should().HaveCount(2, "o histórico de validação não é sobrescrito");
        validacoes[0].Aprovado.Should().BeTrue();
        validacoes[1].Aprovado.Should().BeFalse();
        validacoes[1].Motivo.Should().Be("sem_itens_recentes");
    }

    [Fact]
    public async Task Filtro_por_categoria_valida_so_aquela_categoria()
    {
        await banco.LimparCatalogoAsync();
        await SemearCategoriaAsync("alvo", "https://alvo.test/1.xml");
        await SemearCategoriaAsync("outra", "https://outra.test/1.xml");

        await using var wrapper = new JornalDbContextWrapper(banco);
        var validador = new ValidadorFake(_ => true);
        var servico = Criar(wrapper, validador);

        var relatorio = await servico.ValidarAsync("alvo", atualizar: true, CancellationToken.None);

        validador.Chamadas.Should().Be(1);
        relatorio.Feeds.Should().OnlyContain(f => f.CategoriaSlug == "alvo");
    }

    // RN08 / base do CA-02.4 — a verificação que a spec 02 exige no fim do comando.
    // O bloqueio na API de onboarding é implementado no M3 (spec 03).

    [Fact]
    public async Task RN08_categoria_com_menos_de_dois_feeds_ativos_aparece_no_relatorio()
    {
        await banco.LimparCatalogoAsync();
        await SemearCategoriaAsync("completa", "https://c.test/1.xml", "https://c.test/2.xml");
        await SemearCategoriaAsync("incompleta", "https://i.test/1.xml");

        await using var wrapper = new JornalDbContextWrapper(banco);
        var servico = Criar(wrapper, new ValidadorFake(_ => true));

        var relatorio = await servico.ValidarAsync(null, atualizar: true, CancellationToken.None);

        relatorio.CategoriasAbaixoDoMinimo.Should().HaveCount(1);
        var abaixo = relatorio.CategoriasAbaixoDoMinimo.Single();
        abaixo.Slug.Should().Be("incompleta");
        abaixo.FeedsAtivos.Should().Be(1);
        abaixo.MinimoExigido.Should().Be(2);
    }

    [Fact]
    public async Task RN08_categoria_cai_abaixo_do_minimo_quando_um_feed_e_reprovado()
    {
        await banco.LimparCatalogoAsync();
        await SemearCategoriaAsync("caindo", "https://ok.test/1.xml", "https://no.test/2.xml");

        await using var wrapper = new JornalDbContextWrapper(banco);
        var servico = Criar(wrapper, new ValidadorFake(url => url.Contains("ok.test")));

        var relatorio = await servico.ValidarAsync(null, atualizar: true, CancellationToken.None);

        relatorio.CategoriasAbaixoDoMinimo.Should().ContainSingle(c => c.Slug == "caindo",
            "com 1 de 2 feeds aprovados a categoria fica indisponível no onboarding");
    }

    [Fact]
    public async Task Aprovar_um_feed_zera_as_falhas_consecutivas()
    {
        await banco.LimparCatalogoAsync();
        await SemearCategoriaAsync("recuperando", "https://r.test/1.xml");

        await using (var preparo = banco.CriarContexto())
        {
            var feed = await preparo.Feeds.FirstAsync();
            feed.FalhasConsecutivas = 3;
            await preparo.SaveChangesAsync();
        }

        await using var wrapper = new JornalDbContextWrapper(banco);
        await Criar(wrapper, new ValidadorFake(_ => true))
            .ValidarAsync(null, atualizar: true, CancellationToken.None);

        await using var conferencia = banco.CriarContexto();
        (await conferencia.Feeds.AsNoTracking().FirstAsync())
            .FalhasConsecutivas.Should().Be(0, "o feed voltou a responder");
    }
}
