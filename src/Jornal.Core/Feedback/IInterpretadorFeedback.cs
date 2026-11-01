namespace Jornal.Core.Feedback;

/// <summary>
/// Um ajuste proposto pelo LLM, antes de virar <see cref="AjustePerfil"/> e antes da
/// confirmação do usuário (RF39, RF40).
/// </summary>
public record AjustePerfilProposto(
    TipoAjuste Tipo,
    /// <summary>JSON conforme o tipo — ver a tabela da spec 08.</summary>
    string Payload,
    /// <summary>Cita o trecho do comentário que motivou o ajuste.</summary>
    string Justificativa)
{
    /// <summary>Descrição em linguagem natural, mostrada na tela de confirmação (RF40).</summary>
    public string? Descricao { get; init; }
}

/// <summary>
/// Traduz o comentário livre num conjunto fechado de operações de perfil (RF39).
/// Nada é aplicado aqui — a confirmação é um passo separado (RN10).
/// </summary>
public interface IInterpretadorFeedback
{
    Task<IReadOnlyList<AjustePerfilProposto>> InterpretarAsync(
        Avaliacao avaliacao,
        Perfis.Perfil perfil,
        CancellationToken ct);
}
