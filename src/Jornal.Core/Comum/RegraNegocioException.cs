namespace Jornal.Core.Comum;

/// <summary>
/// Violação de uma regra de negócio do levantamento. O <see cref="Regra"/> carrega o ID
/// (ex.: "RN07"), para que a UI e os testes possam afirmar sobre a regra, não sobre a mensagem.
/// </summary>
public class RegraNegocioException(string regra, string? mensagem = null)
    : Exception(mensagem ?? $"Regra de negócio violada: {regra}")
{
    public string Regra { get; } = regra;
}
