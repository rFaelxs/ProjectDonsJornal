using System.Net.Http.Json;
using System.Text.Json;
using Jornal.Core.Comum;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Xunit.Abstractions;

namespace Jornal.Integration.Tests;

/// <summary>
/// Descobre exatamente o que o OllamaSharp manda para o Ollama. Comparar o vetor dele com
/// o que a API HTTP devolve para o mesmo texto e para texto vazio diz se o problema é o
/// conteúdo enviado ou a leitura da resposta.
/// </summary>
[Trait("Categoria", "Ollama")]
public class DiagnosticoOllamaTests2(ITestOutputHelper saida)
{
    private const string Endpoint = "http://localhost:11434";
    private const string Modelo = "bge-m3";

    private static async Task<float[]> ViaHttpAsync(string texto)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Endpoint) };
        using var resposta = await http.PostAsJsonAsync("/api/embed",
            new { model = Modelo, input = texto });

        resposta.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("embeddings")[0]
            .EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }

    [Fact]
    public async Task De_onde_vem_o_vetor_constante()
    {
        const string futebol = "Flamengo vence o clássico por 2 a 0 no Maracanã.";
        const string economia = "Banco Central mantém a taxa Selic em 10,5% ao ano.";

        // Caminho suspeito: o adaptador IEmbeddingGenerator do OllamaSharp.
        IEmbeddingGenerator<string, Embedding<float>> gerador =
            new OllamaApiClient(new Uri(Endpoint), Modelo);

        var viaSharp = (await gerador.GenerateAsync([futebol],
            cancellationToken: CancellationToken.None))[0].Vector.ToArray();

        // Caminho de referência: a própria API HTTP do Ollama.
        var httpFutebol = await ViaHttpAsync(futebol);
        var httpEconomia = await ViaHttpAsync(economia);

        saida.WriteLine($"HTTP futebol x HTTP economia : " +
                        $"{Vetores.SimilaridadeCosseno(httpFutebol, httpEconomia):F4}");
        saida.WriteLine($"Sharp futebol x HTTP futebol : " +
                        $"{Vetores.SimilaridadeCosseno(viaSharp, httpFutebol):F4}");
        saida.WriteLine($"Sharp futebol x HTTP economia: " +
                        $"{Vetores.SimilaridadeCosseno(viaSharp, httpEconomia):F4}");
        saida.WriteLine($"norma do vetor do Sharp      : " +
                        $"{Math.Sqrt(viaSharp.Sum(f => (double)f * f)):F6}");
        saida.WriteLine($"Sharp[0..4]                  : " +
                        $"{string.Join(", ", viaSharp.Take(4).Select(f => f.ToString("F6")))}");
        saida.WriteLine($"HTTP futebol[0..4]           : " +
                        $"{string.Join(", ", httpFutebol.Take(4).Select(f => f.ToString("F6")))}");
    }
}
