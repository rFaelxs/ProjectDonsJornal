namespace Jornal.Core.Feedback;

/// <summary>
/// Conjunto fechado de operações que o interpretador pode propor (RF39). Qualquer coisa fora
/// desta lista é descartada na validação local.
/// </summary>
public enum TipoAjuste
{
    AdicionarTema,
    RemoverTema,
    MudarPrioridade,
    MudarProfundidade,
    /// <summary>Mantendo exatamente 4 categorias (RN07, CA-08.4).</summary>
    TrocarCategoria
}

public enum StatusAjuste { Proposto, Confirmado, Rejeitado }

/// <summary>
/// Uma mudança de perfil proposta a partir do feedback. Nada altera o perfil sem confirmação
/// explícita do usuário (RF40, CA-08.2).
/// </summary>
public class AjustePerfil
{
    public Guid Id { get; set; }

    public Guid AvaliacaoId { get; set; }
    public Avaliacao? Avaliacao { get; set; }

    public TipoAjuste Tipo { get; set; }

    /// <summary>jsonb. O formato depende do <see cref="Tipo"/> — ver a tabela da spec 08.</summary>
    public required string Payload { get; set; }

    /// <summary>Frase curta que cita o trecho do comentário que motivou o ajuste.</summary>
    public required string Justificativa { get; set; }

    public StatusAjuste Status { get; set; } = StatusAjuste.Proposto;

    /// <summary>Versão do perfil em que o ajuste entrou. Nulo enquanto não confirmado.</summary>
    public int? AplicadoNaVersao { get; set; }
}
