using Jornal.Core.Catalogo;
using Microsoft.Extensions.DependencyInjection;

namespace Jornal.Cli.Comandos;

/// <summary>
/// <c>jornal validar-feeds [--categoria slug] [--atualizar]</c> — RF18 e RN08 (spec 02).
/// Sem <c>--atualizar</c> o comando só relata, sem mexer em <c>feed.ativo</c>.
/// </summary>
public static class ComandoValidarFeeds
{
    public static async Task<int> ExecutarAsync(
        IServiceProvider sp, Argumentos args, CancellationToken ct)
    {
        var categoria = args.Valor("categoria");
        var atualizar = args.TemFlag("atualizar");

        using var escopo = sp.CreateScope();
        var validacao = escopo.ServiceProvider.GetRequiredService<IValidacaoFeeds>();

        Console.WriteLine(atualizar
            ? "Validando feeds e atualizando o campo ativo..."
            : "Validando feeds (relatório apenas; use --atualizar para gravar)...");
        Console.WriteLine();

        var relatorio = await validacao.ValidarAsync(categoria, atualizar, ct);

        if (relatorio.Feeds.Count == 0)
        {
            Console.Error.WriteLine(categoria is null
                ? "Nenhum feed no banco. Rode 'jornal seed' primeiro."
                : $"Nenhum feed na categoria '{categoria}'.");
            return 1;
        }

        ImprimirTabela(relatorio.Feeds);

        Console.WriteLine();
        Console.WriteLine($"{relatorio.Aprovados} aprovados, {relatorio.Reprovados} reprovados.");

        // RN08: a spec 02 exige esta lista no fim do comando.
        if (relatorio.CategoriasAbaixoDoMinimo.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Categorias abaixo do mínimo de feeds ativos (RN08) — ficam");
            Console.WriteLine("indisponíveis para escolha no onboarding:");
            foreach (var c in relatorio.CategoriasAbaixoDoMinimo)
                Console.WriteLine($"  {c.Slug,-24} {c.FeedsAtivos} de {c.MinimoExigido} exigidos");
        }
        else
        {
            Console.WriteLine("Todas as categorias atingem o mínimo de feeds ativos (RN08).");
        }

        return relatorio.Reprovados > 0 ? 1 : 0;
    }

    private static void ImprimirTabela(IReadOnlyList<FeedValidado> feeds)
    {
        var larguraVeiculo = Math.Max(8, feeds.Max(f => f.NomeVeiculo.Length));
        var larguraCategoria = Math.Max(9, feeds.Max(f => f.CategoriaSlug.Length));

        Console.WriteLine($"{"CATEGORIA".PadRight(larguraCategoria)}  " +
                          $"{"VEÍCULO".PadRight(larguraVeiculo)}  " +
                          $"{"RESULTADO",-10} {"FORMATO",-9} {"RECENTES",-9} MOTIVO");
        Console.WriteLine(new string('-', larguraCategoria + larguraVeiculo + 46));

        foreach (var f in feeds)
        {
            var resultado = f.Resultado.Aprovado ? "aprovado" : "REPROVADO";
            var formato = f.Resultado.Formato.ToString().ToLowerInvariant();
            var recentes = f.Resultado.Aprovado || f.Resultado.TotalItens > 0
                ? $"{f.Resultado.ItensRecentes}/{f.Resultado.TotalItens}"
                : "-";
            var motivo = f.Resultado.Motivo ?? "";
            if (f.AtivoAlterado) motivo += motivo.Length > 0 ? " (ativo alterado)" : "(ativo alterado)";

            Console.WriteLine($"{f.CategoriaSlug.PadRight(larguraCategoria)}  " +
                              $"{f.NomeVeiculo.PadRight(larguraVeiculo)}  " +
                              $"{resultado,-10} {formato,-9} {recentes,-9} {motivo}");
        }
    }
}
