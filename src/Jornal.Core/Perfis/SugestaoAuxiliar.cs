namespace Jornal.Core.Perfis;

/// <summary>A que o auxiliar sugerido se refere no catálogo.</summary>
public enum RefTipoSugestao { Categoria, Subtema }

public enum RespostaSugestao { Pendente, Aceito, Recusado }

/// <summary>
/// Uma sugestão de tema auxiliar e a resposta do usuário. Aceitas e recusadas ficam
/// registradas com data (RF11, RF42, CA-03.3).
/// </summary>
public class SugestaoAuxiliar
{
    public Guid Id { get; set; }

    public Guid PerfilId { get; set; }
    public Perfil? Perfil { get; set; }

    public RefTipoSugestao RefTipo { get; set; }

    /// <summary>Id da categoria ou do subtema, conforme <see cref="RefTipo"/>.</summary>
    public Guid RefId { get; set; }

    /// <summary>Similaridade de cosseno que motivou a sugestão. O corte é 0.50 (spec 03, passo 5).</summary>
    public double Similaridade { get; set; }

    public DateTimeOffset SugeridoEm { get; set; }
    public RespostaSugestao Resposta { get; set; } = RespostaSugestao.Pendente;
    public DateTimeOffset? RespondidoEm { get; set; }
}
