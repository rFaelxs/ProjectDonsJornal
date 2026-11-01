using System.Text.Json.Serialization;

namespace Jornal.Core.Catalogo;

/// <summary>
/// Forma do <c>data/catalogo.json</c>, a fonte da verdade do catálogo (spec 02).
/// O arquivo é versionado no repositório e carregado no banco pelo comando <c>seed</c>.
/// </summary>
public record CatalogoArquivo
{
    [JsonPropertyName("versao")]
    public int Versao { get; init; } = 1;

    [JsonPropertyName("categorias")]
    public IReadOnlyList<CategoriaArquivo> Categorias { get; init; } = [];
}

public record CategoriaArquivo
{
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("nome")]
    public required string Nome { get; init; }

    /// <summary>1 a 2 frases. É isto que vira embedding (RF09), não o nome.</summary>
    [JsonPropertyName("descricao")]
    public required string Descricao { get; init; }

    [JsonPropertyName("subtemas")]
    public IReadOnlyList<SubtemaArquivo> Subtemas { get; init; } = [];

    [JsonPropertyName("feeds")]
    public IReadOnlyList<FeedArquivo> Feeds { get; init; } = [];
}

public record SubtemaArquivo
{
    [JsonPropertyName("slug")]
    public required string Slug { get; init; }

    [JsonPropertyName("nome")]
    public required string Nome { get; init; }

    [JsonPropertyName("descricao")]
    public required string Descricao { get; init; }
}

public record FeedArquivo
{
    [JsonPropertyName("veiculo")]
    public required string Veiculo { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// URLs ainda não levantadas ficam com um marcador em vez de endereço. O seed as
    /// ignora, para não gravar lixo no banco.
    /// </summary>
    public bool UrlPendente => string.IsNullOrWhiteSpace(Url) || Url.StartsWith('<');
}
