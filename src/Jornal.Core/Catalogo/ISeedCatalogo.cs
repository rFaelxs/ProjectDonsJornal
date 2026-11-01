namespace Jornal.Core.Catalogo;

/// <summary>O que o seed fez. Serve para o CA-02.1, que exige idempotência.</summary>
public record SeedResumo(
    int CategoriasCriadas,
    int CategoriasAtualizadas,
    int SubtemasCriados,
    int SubtemasAtualizados,
    int FeedsCriados,
    int FeedsIgnoradosSemUrl,
    int EmbeddingsGerados)
{
    /// <summary>Nada mudou — o esperado numa segunda execução sem alteração no arquivo.</summary>
    public bool SemMudancas =>
        CategoriasCriadas == 0 && CategoriasAtualizadas == 0 &&
        SubtemasCriados == 0 && SubtemasAtualizados == 0 &&
        FeedsCriados == 0 && EmbeddingsGerados == 0;
}

/// <summary>
/// Carrega o <c>catalogo.json</c> no banco, com upsert por <c>slug</c> (spec 02).
/// Só regera embedding quando o texto muda, comparando com um hash guardado (CA-02.1).
/// </summary>
public interface ISeedCatalogo
{
    Task<SeedResumo> ExecutarAsync(CatalogoArquivo catalogo, CancellationToken ct);
}
