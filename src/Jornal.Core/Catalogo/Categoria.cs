namespace Jornal.Core.Catalogo;

/// <summary>
/// Tema geral do catálogo (ex.: "Tecnologia e IA"). O usuário escolhe exatamente 4 (RN07).
/// </summary>
public class Categoria
{
    public Guid Id { get; set; }

    /// <summary>Identificador estável usado pelo catalogo.json e pelos prompts (ex.: "tecnologia-ia").</summary>
    public required string Slug { get; set; }

    public required string Nome { get; set; }

    /// <summary>
    /// 1 a 2 frases em linguagem natural. É daqui que sai o embedding (RF09) — o nome sozinho
    /// gera um vetor pobre.
    /// </summary>
    public required string Descricao { get; set; }

    /// <summary>Embedding de "{Nome}: {Descricao}". Nulo até o seed rodar.</summary>
    public float[]? Embedding { get; set; }

    /// <summary>Hash do texto que gerou o embedding, para o seed só regerar quando o texto muda (CA-02.1).</summary>
    public string? EmbeddingHash { get; set; }

    public bool Ativa { get; set; } = true;

    public List<Subtema> Subtemas { get; set; } = [];
    public List<Feed> Feeds { get; set; } = [];
}
