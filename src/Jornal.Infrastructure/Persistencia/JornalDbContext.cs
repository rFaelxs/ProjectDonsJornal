using Jornal.Core.Catalogo;
using Jornal.Core.Ciclo;
using Jornal.Core.Coleta;
using Jornal.Core.Feedback;
using Jornal.Core.Geracao;
using Jornal.Core.Perfis;
using Microsoft.EntityFrameworkCore;

namespace Jornal.Infrastructure.Persistencia;

/// <summary>
/// Banco único: relacional e vetorial no mesmo lugar, para filtrar por data e categoria no
/// mesmo SQL da busca por similaridade (decisão da spec 00).
/// </summary>
public class JornalDbContext(DbContextOptions<JornalDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Dimensão do bge-m3. Trocar o modelo de embedding exige reindexar o acervo, por isso
    /// é constante e não configuração.
    /// </summary>
    public const int DimensoesEmbedding = 1024;

    // Catálogo
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Subtema> Subtemas => Set<Subtema>();
    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<FeedValidacao> FeedValidacoes => Set<FeedValidacao>();
    public DbSet<ColetaFalha> ColetaFalhas => Set<ColetaFalha>();

    // Acervo
    public DbSet<Item> Itens => Set<Item>();
    public DbSet<Trecho> Trechos => Set<Trecho>();

    // Perfil
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfis => Set<Perfil>();
    public DbSet<PerfilCategoria> PerfilCategorias => Set<PerfilCategoria>();
    public DbSet<TemaPerfil> TemasPerfil => Set<TemaPerfil>();
    public DbSet<SugestaoAuxiliar> SugestoesAuxiliares => Set<SugestaoAuxiliar>();

    // Edições e feedback
    public DbSet<Edicao> Edicoes => Set<Edicao>();
    public DbSet<Materia> Materias => Set<Materia>();
    public DbSet<MateriaFonte> MateriaFontes => Set<MateriaFonte>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<AjustePerfil> AjustesPerfil => Set<AjustePerfil>();
    public DbSet<CicloExecucao> CiclosExecucao => Set<CicloExecucao>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Toda data vai para o banco em UTC (spec 01). Sem esta convenção, um feed que
        // publica com offset -03:00 derruba o INSERT: o Npgsql só aceita offset 0 em
        // timestamptz.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<Conversores.DataUtcConverter>();
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<Conversores.DataUtcNullableConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JornalDbContext).Assembly);

        // Tabelas no singular, como a spec 01 as nomeia (`trecho`, `materia_fonte`). Sem isto o
        // EF usa o nome do DbSet e pluraliza, o que quebraria o SQL cru da recuperação
        // (spec 05 consulta `FROM trecho t`).
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
            entidade.SetTableName(ParaSnakeCase(entidade.ClrType.Name));
    }

    private static string ParaSnakeCase(string nome) =>
        System.Text.RegularExpressions.Regex
            .Replace(nome, "(?<!^)([A-Z])", "_$1")
            .ToLowerInvariant();
}
