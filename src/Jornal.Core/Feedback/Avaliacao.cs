using Jornal.Core.Geracao;

namespace Jornal.Core.Feedback;

/// <summary>Reação do usuário a uma seção da edição (RF42).</summary>
public enum AvaliacaoSecao { NaoGostei, Neutro, Gostei }

/// <summary>
/// A avaliação de uma edição. Uma por edição, editável até a próxima ser gerada (spec 08).
/// </summary>
public class Avaliacao
{
    public Guid Id { get; set; }

    public Guid EdicaoId { get; set; }
    public Edicao? Edicao { get; set; }

    /// <summary>1 a 5 estrelas, obrigatória (RF36). Sozinha não gera ajuste automático.</summary>
    public int Nota { get; set; }

    /// <summary>"o que melhorar, o que sobrou, o que faltou" (RF37).</summary>
    public string? Comentario { get; set; }

    /// <summary>"quero ver amanhã…" (RF38). Sempre gera um adicionar_tema específico.</summary>
    public string? NovoTopico { get; set; }

    /// <summary>Por nome de seção. jsonb no banco (RF42).</summary>
    public Dictionary<string, AvaliacaoSecao> SecoesAvaliadas { get; set; } = [];

    public DateTimeOffset CriadaEm { get; set; }

    public List<AjustePerfil> Ajustes { get; set; } = [];
}
