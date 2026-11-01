using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jornal.Infrastructure;

/// <summary>
/// Composição da infraestrutura. Web e Cli chamam isto; nenhum dos dois conhece
/// EF Core, Npgsql ou pgvector diretamente.
/// </summary>
public static class ServicosInfraestrutura
{
    public static IServiceCollection AddJornalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var conexao = configuration.GetConnectionString("Jornal")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Jornal não configurada. Veja o appsettings.json.");

        services.AddDbContext<JornalDbContext>(options => options
            .UseNpgsql(conexao, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention());

        return services;
    }
}
