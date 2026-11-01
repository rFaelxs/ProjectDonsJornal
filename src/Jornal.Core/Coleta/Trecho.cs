using Jornal.Core.Catalogo;

namespace Jornal.Core.Coleta;

/// <summary>
/// Pedaço indexável de um item, com embedding. Na prática quase sempre é o item inteiro,
/// porque só usamos título e resumo (spec 04).
/// </summary>
public class Trecho
{
    public Guid Id { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    /// <summary>Posição dentro do item, começando em 0.</summary>
    public int Ordem { get; set; }

    public required string Texto { get; set; }

    /// <summary>vector(1024) do bge-m3. Indexado com HNSW e vector_cosine_ops.</summary>
    public float[]? Embedding { get; set; }

    // Metadados desnormalizados do item, para filtrar sem join (RF14).
    public DateTimeOffset PublicadoEm { get; set; }
    public Guid CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
    public required string Fonte { get; set; }
}
