using Jornal.Core.Geracao;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jornal.Cli.Comandos;

/// <summary>Comandos de manutenção do banco: <c>migrar</c> e <c>status</c>.</summary>
public static class ComandoBanco
{
    /// <summary>
    /// Aplica as migrations pendentes. Equivale ao <c>dotnet ef database update</c>, mas sem
    /// exigir o SDK do EF na máquina que roda o ciclo.
    /// </summary>
    public static async Task<int> MigrarAsync(IServiceProvider sp, CancellationToken ct)
    {
        using var escopo = sp.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<JornalDbContext>();

        if (!await db.Database.CanConnectAsync(ct))
        {
            Console.Error.WriteLine("Não foi possível conectar ao banco.");
            Console.Error.WriteLine("Suba a infraestrutura com: docker compose up -d postgres");
            return 69;
        }

        var pendentes = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pendentes.Count == 0)
        {
            Console.WriteLine("Banco já está atualizado.");
            return 0;
        }

        Console.WriteLine($"Aplicando {pendentes.Count} migration(s): {string.Join(", ", pendentes)}");
        await db.Database.MigrateAsync(ct);
        Console.WriteLine("Pronto.");
        return 0;
    }

    /// <summary>Diagnóstico rápido: o banco responde, as migrations estão em dia, o acervo tem quanto.</summary>
    public static async Task<int> StatusAsync(IServiceProvider sp, CancellationToken ct)
    {
        using var escopo = sp.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<JornalDbContext>();

        if (!await db.Database.CanConnectAsync(ct))
        {
            Console.Error.WriteLine("Não foi possível conectar ao banco. O docker compose está de pé?");
            return 69;
        }

        var pendentes = (await db.Database.GetPendingMigrationsAsync(ct)).Count();

        Console.WriteLine($"Banco           : conectado");
        Console.WriteLine($"Migrations      : {(pendentes == 0 ? "em dia" : $"{pendentes} pendente(s)")}");
        Console.WriteLine($"Categorias      : {await db.Categorias.CountAsync(ct)}" +
                          $" ({await db.Categorias.CountAsync(c => c.Embedding != null, ct)} com embedding)");
        Console.WriteLine($"Subtemas        : {await db.Subtemas.CountAsync(ct)}");
        Console.WriteLine($"Feeds           : {await db.Feeds.CountAsync(ct)}" +
                          $" ({await db.Feeds.CountAsync(f => f.Ativo, ct)} ativos)");
        Console.WriteLine($"Itens no acervo : {await db.Itens.CountAsync(ct)}");
        Console.WriteLine($"Trechos         : {await db.Trechos.CountAsync(ct)}");
        Console.WriteLine($"Edições prontas : " +
                          $"{await db.Edicoes.CountAsync(e => e.Status == StatusEdicao.Pronta, ct)}");

        // RN08 é o que libera ou bloqueia o onboarding, então entra no status.
        var abaixo = await db.Categorias
            .Where(c => c.Ativa && c.Feeds.Count(f => f.Ativo) < 2)
            .Select(c => c.Slug)
            .OrderBy(s => s)
            .ToListAsync(ct);

        if (abaixo.Count > 0)
            Console.WriteLine($"RN08            : {abaixo.Count} categoria(s) com menos de 2 feeds " +
                              $"ativos ({string.Join(", ", abaixo)})");
        else
            Console.WriteLine("RN08            : todas as categorias com 2+ feeds ativos");

        return 0;
    }
}
