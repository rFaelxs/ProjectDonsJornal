using FluentAssertions;
using Jornal.Core.Catalogo;
using Jornal.Core.Coleta;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jornal.Integration.Tests;

/// <summary>
/// CA-01.2 e a prova de que o conversor <c>float[] ↔ vector</c> funciona em tempo de execução —
/// é ele que mantém Jornal.Core livre de tipos do PostgreSQL (regra da spec 00).
/// </summary>
[Collection(BancoCollection.Nome)]
public class RecuperacaoIndiceTests(BancoFixture banco)
{
    [Fact]
    public async Task Embedding_sobrevive_a_ida_e_volta_no_banco()
    {
        await using var db = banco.CriarContexto();
        var (_, categoriaId, _) = await SemearAsync(db, "roundtrip");

        var vetor = Enumerable.Range(0, 1024).Select(i => i / 1024f).ToArray();

        var item = await db.Itens.FirstAsync(i => i.CategoriaId == categoriaId);
        db.Trechos.Add(new Trecho
        {
            ItemId = item.Id,
            Ordem = 99,
            Texto = "trecho de ida e volta",
            Embedding = vetor,
            PublicadoEm = DateTimeOffset.UtcNow,
            CategoriaId = categoriaId,
            Fonte = "Teste"
        });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var lido = await db.Trechos.FirstAsync(t => t.Ordem == 99);

        lido.Embedding.Should().NotBeNull();
        lido.Embedding!.Should().HaveCount(1024);
        lido.Embedding.Should().Equal(vetor);
    }

    [Fact]
    public async Task CA_01_2_consulta_de_similaridade_com_filtros_usa_indice()
    {
        await using var db = banco.CriarContexto();
        var (_, categoriaId, itemId) = await SemearAsync(db, "explain");

        // O planejador só prefere índice com volume. 3 mil trechos já passam do ponto
        // em que o seq scan deixa de ser mais barato.
        await InserirTrechosEmMassaAsync(banco.ConnectionString, itemId, categoriaId, 3000);

        var plano = await ExplicarConsultaDaPautaAsync(banco.ConnectionString, categoriaId);

        // A consulta é a do passo 1 da spec 05: janela de 24h, filtro de categoria e
        // ordenação por distância de cosseno.
        plano.Should().NotContain("Seq Scan on trecho",
            $"a consulta da pauta precisa usar índice. Plano obtido:\n{plano}");
        plano.Should().MatchRegex("ix_trecho_(embedding|categoria_id_publicado_em|publicado_em)",
            $"o plano deve citar um dos índices da spec 01. Plano obtido:\n{plano}");
    }

    [Fact]
    public async Task CA_01_2_busca_vetorial_pura_usa_o_indice_hnsw()
    {
        await using var db = banco.CriarContexto();
        var (_, categoriaId, itemId) = await SemearAsync(db, "hnsw");
        await InserirTrechosEmMassaAsync(banco.ConnectionString, itemId, categoriaId, 3000);

        var plano = await ExplicarAsync(banco.ConnectionString, $"""
            EXPLAIN SELECT id FROM trecho
            ORDER BY embedding <=> '{VetorLiteral(0.5f)}'::vector
            LIMIT 12
            """);

        plano.Should().Contain("ix_trecho_embedding",
            $"sem o HNSW a busca vetorial vira varredura completa. Plano obtido:\n{plano}");
    }

    // --- apoio ---

    private static async Task<(Guid feedId, Guid categoriaId, Guid itemId)> SemearAsync(
        DbContext contexto, string sufixo)
    {
        var db = (Jornal.Infrastructure.Persistencia.JornalDbContext)contexto;

        var categoria = new Categoria
        {
            Slug = $"cat-{sufixo}",
            Nome = $"Categoria {sufixo}",
            Descricao = "Categoria usada pelos testes de índice."
        };
        var feed = new Feed
        {
            Categoria = categoria,
            NomeVeiculo = "Veículo de teste",
            Url = $"https://exemplo.test/{sufixo}.xml",
            Ativo = true
        };
        var item = new Item
        {
            Feed = feed,
            Categoria = categoria,
            Fonte = "Veículo de teste",
            Titulo = "Título de teste",
            Resumo = "Resumo de teste.",
            Link = $"https://exemplo.test/{sufixo}/noticia",
            LinkHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(sufixo))).ToLowerInvariant(),
            PublicadoEm = DateTimeOffset.UtcNow,
            ColetadoEm = DateTimeOffset.UtcNow
        };

        db.Itens.Add(item);
        await db.SaveChangesAsync();
        return (feed.Id, categoria.Id, item.Id);
    }

    /// <summary>
    /// Insere em massa pelo SQL, gerando os vetores no próprio PostgreSQL. Passar 3 mil
    /// embeddings de 1024 dimensões pelo EF deixaria o teste lento sem ganho nenhum.
    /// </summary>
    private static async Task InserirTrechosEmMassaAsync(
        string conexaoTexto, Guid itemId, Guid categoriaId, int quantidade)
    {
        await using var conexao = new NpgsqlConnection(conexaoTexto);
        await conexao.OpenAsync();

        await using var cmd = new NpgsqlCommand("""
            INSERT INTO trecho (id, item_id, ordem, texto, embedding, publicado_em, categoria_id, fonte)
            SELECT gen_random_uuid(), @itemId, 1000 + g, 'trecho ' || g,
                   ('[' || array_to_string(array_agg_vetor.v, ',') || ']')::vector,
                   now() - (g || ' minutes')::interval,
                   @categoriaId, 'Veículo de teste'
            FROM generate_series(1, @quantidade) g
            CROSS JOIN LATERAL (
                SELECT array_agg(random()) AS v FROM generate_series(1, 1024)
            ) AS array_agg_vetor
            """, conexao);

        cmd.Parameters.AddWithValue("itemId", itemId);
        cmd.Parameters.AddWithValue("categoriaId", categoriaId);
        cmd.Parameters.AddWithValue("quantidade", quantidade);
        await cmd.ExecuteNonQueryAsync();

        // Sem estatísticas atualizadas o planejador decide no escuro.
        await using var analyze = new NpgsqlCommand("ANALYZE trecho", conexao);
        await analyze.ExecuteNonQueryAsync();
    }

    private static Task<string> ExplicarConsultaDaPautaAsync(string conexaoTexto, Guid categoriaId)
        => ExplicarAsync(conexaoTexto, $"""
            EXPLAIN SELECT t.id, 1 - (t.embedding <=> '{VetorLiteral(0.5f)}'::vector) AS sim
            FROM trecho t
            WHERE t.publicado_em >= now() - interval '24 hours'
              AND t.categoria_id = ANY(ARRAY['{categoriaId}']::uuid[])
            ORDER BY t.embedding <=> '{VetorLiteral(0.5f)}'::vector
            LIMIT 12
            """);

    private static async Task<string> ExplicarAsync(string conexaoTexto, string sql)
    {
        await using var conexao = new NpgsqlConnection(conexaoTexto);
        await conexao.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conexao);
        await using var leitor = await cmd.ExecuteReaderAsync();

        var linhas = new List<string>();
        while (await leitor.ReadAsync()) linhas.Add(leitor.GetString(0));
        return string.Join('\n', linhas);
    }

    private static string VetorLiteral(float valor) =>
        '[' + string.Join(',', Enumerable.Repeat(
            valor.ToString(System.Globalization.CultureInfo.InvariantCulture), 1024)) + ']';
}
