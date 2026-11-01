using FluentAssertions;
using Jornal.Core.Comum;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Xunit.Abstractions;

namespace Jornal.Integration.Tests;

/// <summary>
/// Testes contra o Ollama real. Ficam fora da coleção do banco e só valem quando o
/// Ollama está de pé com o bge-m3 — servem para diagnosticar o caminho
/// OllamaSharp → IEmbeddingGenerator, que é onde um lote mal montado se esconderia.
/// </summary>
[Trait("Categoria", "Ollama")]
public class DiagnosticoOllamaTests(ITestOutputHelper saida)
{
    private static IEmbeddingGenerator<string, Embedding<float>> Gerador() =>
        new OllamaApiClient(new Uri("http://localhost:11434"), "bge-m3");

    [Fact]
    public async Task Lote_devolve_um_embedding_distinto_por_texto()
    {
        string[] textos =
        [
            "Flamengo vence o clássico por 2 a 0 no Maracanã.",
            "Banco Central mantém a taxa Selic em 10,5% ao ano.",
            "Novo modelo de IA da OpenAI promete raciocínio melhor."
        ];

        var vetores = await Gerador().GenerateAsync(textos, cancellationToken: CancellationToken.None);

        vetores.Count.Should().Be(3);

        var a = vetores[0].Vector.ToArray();
        var b = vetores[1].Vector.ToArray();
        var c = vetores[2].Vector.ToArray();

        a.Should().HaveCount(1024);

        var ab = Vetores.SimilaridadeCosseno(a, b);
        var ac = Vetores.SimilaridadeCosseno(a, c);
        var bc = Vetores.SimilaridadeCosseno(b, c);

        saida.WriteLine($"futebol x economia : {ab:F4}");
        saida.WriteLine($"futebol x IA       : {ac:F4}");
        saida.WriteLine($"economia x IA      : {bc:F4}");

        // Se o lote estivesse devolvendo o mesmo vetor repetido, estas similaridades
        // seriam 1.0 e tudo viraria duplicata.
        ab.Should().BeLessThan(0.93, "notícias de assuntos diferentes não são duplicatas");
        ac.Should().BeLessThan(0.93);
        bc.Should().BeLessThan(0.93);
    }

    [Fact]
    public async Task Texto_identico_gera_vetor_identico()
    {
        const string texto = "Banco Central mantém a taxa Selic em 10,5% ao ano.";

        var vetores = await Gerador().GenerateAsync([texto, texto],
            cancellationToken: CancellationToken.None);

        var similaridade = Vetores.SimilaridadeCosseno(
            vetores[0].Vector.ToArray(), vetores[1].Vector.ToArray());

        saida.WriteLine($"mesmo texto: {similaridade:F6}");
        similaridade.Should().BeGreaterThan(0.999);
    }

    [Fact]
    public async Task Uma_chamada_por_texto_bate_com_a_chamada_em_lote()
    {
        string[] textos =
        [
            "Flamengo vence o clássico por 2 a 0 no Maracanã.",
            "Banco Central mantém a taxa Selic em 10,5% ao ano."
        ];

        var gerador = Gerador();
        var emLote = await gerador.GenerateAsync(textos, cancellationToken: CancellationToken.None);

        var individuais = new List<float[]>();
        foreach (var texto in textos)
        {
            var um = await gerador.GenerateAsync([texto], cancellationToken: CancellationToken.None);
            individuais.Add(um[0].Vector.ToArray());
        }

        for (var i = 0; i < textos.Length; i++)
        {
            var similaridade = Vetores.SimilaridadeCosseno(
                emLote[i].Vector.ToArray(), individuais[i]);

            saida.WriteLine($"texto {i} lote x individual: {similaridade:F6}");
            similaridade.Should().BeGreaterThan(0.999,
                "o lote precisa preservar a ordem e o conteúdo de cada texto");
        }
    }
}
