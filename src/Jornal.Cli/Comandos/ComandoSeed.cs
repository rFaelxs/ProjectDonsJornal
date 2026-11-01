using System.Text.Json;
using Jornal.Core.Catalogo;
using Microsoft.Extensions.DependencyInjection;

namespace Jornal.Cli.Comandos;

/// <summary>
/// <c>jornal seed [--arquivo data/catalogo.json]</c> — carrega o catálogo no banco (spec 02).
/// </summary>
public static class ComandoSeed
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<int> ExecutarAsync(
        IServiceProvider sp, Argumentos args, CancellationToken ct)
    {
        var caminho = args.Valor("arquivo") ?? "data/catalogo.json";

        if (!File.Exists(caminho))
        {
            Console.Error.WriteLine($"Arquivo não encontrado: {Path.GetFullPath(caminho)}");
            Console.Error.WriteLine("Rode da raiz do repositório ou passe --arquivo <caminho>.");
            return 66;
        }

        CatalogoArquivo? catalogo;
        try
        {
            await using var fluxo = File.OpenRead(caminho);
            catalogo = await JsonSerializer.DeserializeAsync<CatalogoArquivo>(fluxo, Json, ct);
        }
        catch (JsonException e)
        {
            Console.Error.WriteLine($"JSON inválido em {caminho}: {e.Message}");
            return 65;
        }

        if (catalogo is null || catalogo.Categorias.Count == 0)
        {
            Console.Error.WriteLine($"{caminho} não tem nenhuma categoria.");
            return 65;
        }

        Console.WriteLine($"Lendo {caminho} (versão {catalogo.Versao}, " +
                          $"{catalogo.Categorias.Count} categorias)...");

        using var escopo = sp.CreateScope();
        var seed = escopo.ServiceProvider.GetRequiredService<ISeedCatalogo>();

        SeedResumo resumo;
        try
        {
            resumo = await seed.ExecutarAsync(catalogo, ct);
        }
        catch (HttpRequestException e)
        {
            Console.Error.WriteLine($"Não foi possível falar com o Ollama: {e.Message}");
            Console.Error.WriteLine(
                "O seed gera embeddings das descrições (RF09). Suba o Ollama e baixe o bge-m3:");
            Console.Error.WriteLine("  docker compose up -d ollama");
            Console.Error.WriteLine("  docker compose --profile setup run --rm ollama-pull");
            return 69;
        }

        Console.WriteLine();
        Console.WriteLine($"  Categorias   : {resumo.CategoriasCriadas} criadas, " +
                          $"{resumo.CategoriasAtualizadas} atualizadas");
        Console.WriteLine($"  Subtemas     : {resumo.SubtemasCriados} criados, " +
                          $"{resumo.SubtemasAtualizados} atualizados");
        Console.WriteLine($"  Feeds        : {resumo.FeedsCriados} criados (todos inativos)");
        Console.WriteLine($"  Embeddings   : {resumo.EmbeddingsGerados} gerados");

        if (resumo.FeedsIgnoradosSemUrl > 0)
            Console.WriteLine($"  Sem URL      : {resumo.FeedsIgnoradosSemUrl} feeds ignorados");

        if (resumo.SemMudancas)
            Console.WriteLine("\nNada mudou — o catálogo no banco já está igual ao arquivo.");
        else
            Console.WriteLine("\nFeeds entram inativos. Rode 'jornal validar-feeds --atualizar'.");

        return 0;
    }
}
