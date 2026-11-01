namespace Jornal.Core.Catalogo;

/// <summary>Uma linha da tabela que o comando <c>validar-feeds</c> imprime.</summary>
public record FeedValidado(
    Guid FeedId,
    string CategoriaSlug,
    string NomeVeiculo,
    string Url,
    FeedValidationResult Resultado,
    /// <summary>True quando o comando rodou com <c>--atualizar</c> e mexeu em <c>feed.ativo</c>.</summary>
    bool AtivoAlterado);

/// <summary>
/// Relatório completo. A lista de categorias abaixo do mínimo é a verificação de RN08
/// que a spec 02 exige no fim do comando.
/// </summary>
public record ValidacaoFeedsRelatorio(
    IReadOnlyList<FeedValidado> Feeds,
    IReadOnlyList<CategoriaSemFeedsSuficientes> CategoriasAbaixoDoMinimo)
{
    public int Aprovados => Feeds.Count(f => f.Resultado.Aprovado);
    public int Reprovados => Feeds.Count(f => !f.Resultado.Aprovado);
}

/// <summary>
/// Categoria que não atinge o mínimo de feeds ativos. Fica indisponível para escolha no
/// onboarding, com o motivo (RN08, CA-02.4).
/// </summary>
public record CategoriaSemFeedsSuficientes(
    string Slug,
    string Nome,
    int FeedsAtivos,
    int MinimoExigido);

/// <summary>Executa o validador sobre os feeds do catálogo (RF18).</summary>
public interface IValidacaoFeeds
{
    Task<ValidacaoFeedsRelatorio> ValidarAsync(
        string? categoriaSlug,
        bool atualizar,
        CancellationToken ct);
}
