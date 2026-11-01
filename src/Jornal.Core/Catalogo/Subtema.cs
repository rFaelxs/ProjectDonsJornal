namespace Jornal.Core.Catalogo;

/// <summary>Recorte dentro de uma categoria (ex.: "cibersegurança"). Opcional no perfil.</summary>
public class Subtema
{
    public Guid Id { get; set; }
    public Guid CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public required string Slug { get; set; }
    public required string Nome { get; set; }

    /// <summary>Vira embedding junto com o nome (RF09).</summary>
    public required string Descricao { get; set; }

    public float[]? Embedding { get; set; }
    public string? EmbeddingHash { get; set; }
}
