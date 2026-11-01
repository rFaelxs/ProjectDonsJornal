namespace Jornal.Core.Comum;

/// <summary>
/// Operações sobre embeddings. Função pura, para que os limiares de RF15 e RF20 sejam
/// testáveis sem banco.
/// </summary>
public static class Vetores
{
    /// <summary>
    /// Similaridade de cosseno, de -1 a 1. É a métrica usada em toda a recuperação, e a
    /// mesma do índice HNSW (<c>vector_cosine_ops</c>).
    /// </summary>
    public static double SimilaridadeCosseno(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException(
                $"Dimensões diferentes: {a.Length} e {b.Length}.", nameof(b));

        double produto = 0, normaA = 0, normaB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            produto += (double)a[i] * b[i];
            normaA += (double)a[i] * a[i];
            normaB += (double)b[i] * b[i];
        }

        if (normaA == 0 || normaB == 0) return 0;

        return produto / (Math.Sqrt(normaA) * Math.Sqrt(normaB));
    }

    /// <summary>
    /// Centróide de um conjunto de vetores, usado para representar um assunto na
    /// recuperação (RF20, RF21).
    /// </summary>
    public static float[] Centroide(IReadOnlyList<float[]> vetores)
    {
        if (vetores.Count == 0) throw new ArgumentException("Conjunto vazio.", nameof(vetores));

        var dimensoes = vetores[0].Length;
        var soma = new double[dimensoes];

        foreach (var vetor in vetores)
        {
            if (vetor.Length != dimensoes)
                throw new ArgumentException("Vetores com dimensões diferentes.", nameof(vetores));

            for (var i = 0; i < dimensoes; i++) soma[i] += vetor[i];
        }

        var centroide = new float[dimensoes];
        for (var i = 0; i < dimensoes; i++) centroide[i] = (float)(soma[i] / vetores.Count);

        return centroide;
    }
}
