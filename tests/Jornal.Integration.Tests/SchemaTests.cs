using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jornal.Integration.Tests;

/// <summary>Critérios de aceite da spec 01.</summary>
[Collection(BancoCollection.Nome)]
public class SchemaTests(BancoFixture banco)
{
    /// <summary>As 18 tabelas da spec 01, nos nomes exatos em que ela as escreve.</summary>
    private static readonly string[] TabelasEsperadas =
    [
        "categoria", "subtema", "feed", "feed_validacao", "coleta_falha",
        "item", "trecho",
        "usuario", "perfil", "perfil_categoria", "tema_perfil", "sugestao_auxiliar",
        "edicao", "materia", "materia_fonte", "avaliacao", "ajuste_perfil", "ciclo_execucao"
    ];

    // CA-01.1 — a migration cria o schema completo, com a extensão vector e o índice HNSW.

    [Fact]
    public async Task CA_01_1_extensao_vector_instalada()
    {
        await using var db = banco.CriarContexto();

        var versao = await EscalarAsync<string>(banco.ConnectionString,
            "SELECT extversion FROM pg_extension WHERE extname = 'vector'");

        versao.Should().NotBeNull("a migration precisa rodar CREATE EXTENSION vector");
    }

    [Fact]
    public async Task CA_01_1_todas_as_tabelas_da_spec_existem()
    {
        await using var db = banco.CriarContexto();

        var tabelas = new List<string>();
        await using var conexao = new NpgsqlConnection(banco.ConnectionString);
        await conexao.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'public'", conexao);
        await using var leitor = await cmd.ExecuteReaderAsync();
        while (await leitor.ReadAsync()) tabelas.Add(leitor.GetString(0));

        tabelas.Should().Contain(TabelasEsperadas);
    }

    [Fact]
    public async Task CA_01_1_indice_hnsw_com_vector_cosine_ops()
    {
        await using var db = banco.CriarContexto();

        var ddl = await EscalarAsync<string>(banco.ConnectionString,
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'trecho' AND indexdef LIKE '%hnsw%'");

        ddl.Should().NotBeNull("a spec 01 exige índice HNSW em trecho.embedding");
        ddl.Should().Contain("vector_cosine_ops",
            "a recuperação mede distância de cosseno (spec 05)");
    }

    [Fact]
    public async Task CA_01_1_embedding_tem_1024_dimensoes()
    {
        await using var db = banco.CriarContexto();

        // atttypmod do tipo vector guarda as dimensões.
        var dimensoes = await EscalarAsync<int?>(banco.ConnectionString, """
            SELECT a.atttypmod
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            WHERE c.relname = 'trecho' AND a.attname = 'embedding'
            """);

        dimensoes.Should().Be(1024, "é a dimensão do bge-m3");
    }

    [Fact]
    public async Task CA_01_1_colunas_de_data_sao_timestamptz()
    {
        await using var db = banco.CriarContexto();

        var tipo = await EscalarAsync<string>(banco.ConnectionString, """
            SELECT data_type FROM information_schema.columns
            WHERE table_name = 'trecho' AND column_name = 'publicado_em'
            """);

        tipo.Should().Be("timestamp with time zone",
            "a spec 01 guarda datas em UTC e exibe em America/Sao_Paulo");
    }

    private static async Task<T?> EscalarAsync<T>(string conexaoTexto, string sql)
    {
        await using var conexao = new NpgsqlConnection(conexaoTexto);
        await conexao.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conexao);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado is null or DBNull ? default : (T)resultado;
    }
}
