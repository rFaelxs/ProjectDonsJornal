using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Jornal.Integration.Tests;

/// <summary>
/// Sobe um PostgreSQL 17 com pgvector em container, uma vez por coleção de testes.
/// Usa a imagem oficial do pgvector para não precisar compilar a extensão.
/// </summary>
public class BancoFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("jornal_teste")
        .WithUsername("jornal")
        .WithPassword("jornal")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // CA-01.1: é a própria migration que cria a extensão e o schema.
        await using var db = CriarContexto();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Zera o catálogo e tudo que depende dele. Os testes de seed precisam de um banco
    /// limpo; os outros criam os próprios dados, então não se incomodam.
    /// </summary>
    public async Task LimparCatalogoAsync()
    {
        await using var db = CriarContexto();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE categoria, subtema, feed, feed_validacao, item, trecho CASCADE");
    }

    public JornalDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<JornalDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new JornalDbContext(options);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Nome)]
public class BancoCollection : ICollectionFixture<BancoFixture>
{
    public const string Nome = "banco";
}
