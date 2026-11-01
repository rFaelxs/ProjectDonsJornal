using Microsoft.Extensions.AI;

namespace Jornal.Integration.Tests.Apoio;

/// <summary>
/// Gera embeddings determinísticos a partir do hash do texto. Os testes precisam de
/// vetores estáveis e de uma contagem de chamadas — é ela que prova a idempotência do
/// seed (CA-02.1) sem depender do Ollama.
/// </summary>
public class GeradorEmbeddingFake(int dimensoes = 1024)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>Quantos textos passaram pelo gerador desde a criação.</summary>
    public int TextosGerados { get; private set; }

    /// <summary>Quantas vezes <see cref="GenerateAsync"/> foi chamado.</summary>
    public int Chamadas { get; private set; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var textos = values.ToList();
        Chamadas++;
        TextosGerados += textos.Count;

        var resultado = new GeneratedEmbeddings<Embedding<float>>(
            textos.Select(t => new Embedding<float>(VetorDeterministico(t))));

        return Task.FromResult(resultado);
    }

    /// <summary>
    /// Mesmo texto, mesmo vetor, sempre. Usa o hash para espalhar os valores, de modo que
    /// textos diferentes fiquem distantes e o mesmo texto fique idêntico.
    /// </summary>
    private float[] VetorDeterministico(string texto)
    {
        var semente = texto.GetHashCode(StringComparison.Ordinal);
        var aleatorio = new Random(semente);
        var vetor = new float[dimensoes];

        for (var i = 0; i < dimensoes; i++)
            vetor[i] = (float)(aleatorio.NextDouble() * 2 - 1);

        // Normaliza: a similaridade de cosseno fica previsível com vetores unitários.
        var norma = MathF.Sqrt(vetor.Sum(v => v * v));
        for (var i = 0; i < dimensoes; i++) vetor[i] /= norma;

        return vetor;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
