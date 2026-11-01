namespace Jornal.Core.Catalogo;

/// <summary>Resultado de uma validação de URL de feed (RF18, spec 02).</summary>
public record FeedValidationResult(
    bool Aprovado,
    int? HttpStatus,
    FormatoFeed Formato,
    int ItensRecentes,
    int TotalItens,
    /// <summary>http_&lt;status&gt;, timeout, formato_invalido, sem_itens_recentes, campos_incompletos.</summary>
    string? Motivo)
{
    public static FeedValidationResult Reprovado(string motivo, int? httpStatus = null, FormatoFeed formato = FormatoFeed.Invalido)
        => new(false, httpStatus, formato, 0, 0, motivo);
}

/// <summary>Checa se uma URL serve como feed antes de ela entrar como ativa (RN08).</summary>
public interface IFeedValidator
{
    Task<FeedValidationResult> ValidateAsync(string url, CancellationToken ct);
}
