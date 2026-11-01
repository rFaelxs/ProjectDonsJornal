using Jornal.Core.Catalogo;
using Microsoft.Extensions.DependencyInjection;

namespace Jornal.Cli.Comandos;

/// <summary>
/// <c>jornal validar-url &lt;url&gt; [&lt;url&gt; ...]</c> — roda o validador da spec 02 em URLs
/// soltas, sem tocar no banco. É a ferramenta para levantar candidatos antes de escrevê-los
/// no catalogo.json, já que nenhuma URL deve entrar sem passar pelo validador (RN08).
/// </summary>
public static class ComandoValidarUrl
{
    public static async Task<int> ExecutarAsync(
        IServiceProvider sp, string[] args, CancellationToken ct)
    {
        var urls = args.Skip(1).Where(a => !a.StartsWith("--")).ToArray();

        if (urls.Length == 0)
        {
            Console.Error.WriteLine("Uso: jornal validar-url <url> [<url> ...]");
            return 64;
        }

        using var escopo = sp.CreateScope();
        var validador = escopo.ServiceProvider.GetRequiredService<IFeedValidator>();

        var aprovados = 0;

        foreach (var url in urls)
        {
            ct.ThrowIfCancellationRequested();

            var r = await validador.ValidateAsync(url, ct);
            if (r.Aprovado) aprovados++;

            var marca = r.Aprovado ? "OK  " : "FALHA";
            var detalhe = r.Aprovado
                ? $"{r.Formato.ToString().ToLowerInvariant()}, " +
                  $"{r.ItensRecentes}/{r.TotalItens} itens recentes"
                : $"{r.Motivo}" + (r.HttpStatus is { } s ? $" (HTTP {s})" : "");

            Console.WriteLine($"{marca} {url}");
            Console.WriteLine($"      {detalhe}");
        }

        Console.WriteLine();
        Console.WriteLine($"{aprovados} de {urls.Length} aprovados.");
        return aprovados == urls.Length ? 0 : 1;
    }
}
