using FluentAssertions;
using Jornal.Core.Catalogo;
using Jornal.Infrastructure.Catalogo;
using Jornal.Infrastructure.Persistencia;
using Jornal.Integration.Tests.Apoio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jornal.Integration.Tests;

/// <summary>Critérios de aceite do seed do catálogo (spec 02).</summary>
[Collection(BancoCollection.Nome)]
public class SeedCatalogoTests(BancoFixture banco)
{
    private static CatalogoArquivo Catalogo(string descricaoCategoria = "Descrição da categoria de teste.") =>
        new()
        {
            Versao = 1,
            Categorias =
            [
                new CategoriaArquivo
                {
                    Slug = "tecnologia-ia",
                    Nome = "Tecnologia e IA",
                    Descricao = descricaoCategoria,
                    Subtemas =
                    [
                        new SubtemaArquivo
                        {
                            Slug = "ciberseguranca",
                            Nome = "Cibersegurança",
                            Descricao = "Vazamentos, ataques e defesa."
                        }
                    ],
                    Feeds =
                    [
                        new FeedArquivo { Veiculo = "Canaltech", Url = "https://exemplo.test/canaltech.xml" },
                        new FeedArquivo { Veiculo = "Tecnoblog", Url = "https://exemplo.test/tecnoblog.xml" },
                        // Marcador de URL não levantada: o seed deve ignorar.
                        new FeedArquivo { Veiculo = "A definir", Url = "<a confirmar no M1>" }
                    ]
                }
            ]
        };

    private (SeedCatalogoService Seed, GeradorEmbeddingFake Gerador, JornalDbContext Db)
        Montar()
    {
        var db = banco.CriarContexto();
        var gerador = new GeradorEmbeddingFake();
        var seed = new SeedCatalogoService(db, gerador, NullLogger<SeedCatalogoService>.Instance);
        return (seed, gerador, db);
    }

    // CA-02.1 — o seed é idempotente: rodar duas vezes não duplica dados nem regera embeddings.

    [Fact]
    public async Task CA_02_1_primeira_execucao_cria_tudo_e_gera_embeddings()
    {
        await banco.LimparCatalogoAsync();
        var (seed, gerador, db) = Montar();
        await using var _ = db;

        var resumo = await seed.ExecutarAsync(Catalogo(), CancellationToken.None);

        resumo.CategoriasCriadas.Should().Be(1);
        resumo.SubtemasCriados.Should().Be(1);
        resumo.FeedsCriados.Should().Be(2);
        resumo.FeedsIgnoradosSemUrl.Should().Be(1, "a URL com marcador não entra no banco");

        // Um embedding para a categoria, outro para o subtema.
        resumo.EmbeddingsGerados.Should().Be(2);
        gerador.TextosGerados.Should().Be(2);

        // Uma chamada só: o gerador recebe os textos em lote.
        gerador.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task CA_02_1_segunda_execucao_nao_duplica_nem_regera_embeddings()
    {
        await banco.LimparCatalogoAsync();

        var (primeiro, _, db1) = Montar();
        await primeiro.ExecutarAsync(Catalogo(), CancellationToken.None);
        await db1.DisposeAsync();

        var (segundo, gerador, db2) = Montar();
        await using var _ = db2;

        var resumo = await segundo.ExecutarAsync(Catalogo(), CancellationToken.None);

        resumo.SemMudancas.Should().BeTrue();
        resumo.EmbeddingsGerados.Should().Be(0, "o hash do texto não mudou");
        gerador.Chamadas.Should().Be(0, "nem deve chamar o gerador");

        (await db2.Categorias.CountAsync()).Should().Be(1);
        (await db2.Subtemas.CountAsync()).Should().Be(1);
        (await db2.Feeds.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task CA_02_1_mudar_a_descricao_regera_o_embeddding_daquele_item()
    {
        await banco.LimparCatalogoAsync();

        var (primeiro, _, db1) = Montar();
        await primeiro.ExecutarAsync(Catalogo(), CancellationToken.None);
        var embeddingAntigo = (await db1.Categorias.AsNoTracking().FirstAsync()).Embedding;
        await db1.DisposeAsync();

        var (segundo, gerador, db2) = Montar();
        await using var _ = db2;

        var resumo = await segundo.ExecutarAsync(
            Catalogo("Descrição NOVA, que muda o hash."), CancellationToken.None);

        resumo.CategoriasAtualizadas.Should().Be(1);
        resumo.EmbeddingsGerados.Should().Be(1, "só a categoria mudou; o subtema não");

        var categoria = await db2.Categorias.AsNoTracking().FirstAsync();
        categoria.Descricao.Should().Be("Descrição NOVA, que muda o hash.");
        categoria.Embedding.Should().NotBeEquivalentTo(embeddingAntigo);
    }

    [Fact]
    public async Task Feeds_entram_inativos_porque_so_o_validador_os_liga()
    {
        await banco.LimparCatalogoAsync();
        var (seed, _, db) = Montar();
        await using var _ = db;

        await seed.ExecutarAsync(Catalogo(), CancellationToken.None);

        var feeds = await db.Feeds.AsNoTracking().ToListAsync();
        feeds.Should().OnlyContain(f => !f.Ativo,
            "RN08: nenhuma URL entra como ativa sem passar pelo validador");
        feeds.Should().OnlyContain(f => f.ValidadoEm == null);
    }

    [Fact]
    public async Task Embedding_sai_do_nome_mais_a_descricao_nao_do_nome_sozinho()
    {
        await banco.LimparCatalogoAsync();
        var (seed, gerador, db) = Montar();
        await using var _ = db;

        await seed.ExecutarAsync(Catalogo(), CancellationToken.None);

        var categoria = await db.Categorias.AsNoTracking().FirstAsync();

        // RF09: o texto embutido é "{nome}: {descricao}". Comparar com o vetor que o
        // gerador produz para esse texto exato prova a composição.
        var esperado = await gerador.GenerateAsync(
            ["Tecnologia e IA: Descrição da categoria de teste."],
            cancellationToken: CancellationToken.None);

        categoria.Embedding.Should().Equal(esperado[0].Vector.ToArray());
    }

    [Fact]
    public async Task Todas_as_categorias_e_subtemas_ficam_com_embedding()
    {
        await banco.LimparCatalogoAsync();
        var (seed, _, db) = Montar();
        await using var _ = db;

        await seed.ExecutarAsync(Catalogo(), CancellationToken.None);

        (await db.Categorias.CountAsync(c => c.Embedding == null)).Should().Be(0);
        (await db.Subtemas.CountAsync(s => s.Embedding == null)).Should().Be(0);
    }
}
