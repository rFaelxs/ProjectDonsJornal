using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Jornal.Infrastructure.Persistencia;

/// <summary>
/// Usada só pelo <c>dotnet ef</c>. Permite gerar migrations sem subir o Jornal.Web,
/// e sem precisar de um banco de pé.
/// </summary>
public class JornalDbContextFactory : IDesignTimeDbContextFactory<JornalDbContext>
{
    public JornalDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("JORNAL_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=jornal;Username=jornal;Password=jornal";

        var options = new DbContextOptionsBuilder<JornalDbContext>()
            .UseNpgsql(conexao, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new JornalDbContext(options);
    }
}
