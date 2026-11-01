using Jornal.Core.Feedback;
using Jornal.Core.Perfis;

namespace Jornal.Core.Geracao;

public enum StatusEdicao { Gerando, Pronta, Falhou }

/// <summary>
/// O jornal de um dia. Fecha com a marca de fim (RF32) e no máximo 2 páginas A4 (RNF05).
/// </summary>
public class Edicao
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Sequencial por usuário, começando em 1. Aparece na marca de fim.</summary>
    public int Numero { get; set; }

    /// <summary>O dia que a edição cobre, no fuso do perfil. Único por usuário.</summary>
    public DateOnly DataRef { get; set; }

    /// <summary>Versão do perfil que gerou esta edição — permite reconstruir o histórico (RF41).</summary>
    public int PerfilVersao { get; set; }

    public StatusEdicao Status { get; set; } = StatusEdicao.Gerando;

    /// <summary>Caminho relativo do PDF entregue. Nulo enquanto gera ou se falhou (RN09).</summary>
    public string? PdfCaminho { get; set; }

    public int Paginas { get; set; }
    public DateTimeOffset? GeradaEm { get; set; }

    /// <summary>Versão do template de manchetes usado, ex.: "manchetes.v1" (RF28).</summary>
    public string? PromptVersao { get; set; }

    /// <summary>Modelo de chat que redigiu, ex.: "qwen2.5:7b-instruct". Rastreabilidade da demo.</summary>
    public string? ModeloChat { get; set; }

    public List<Materia> Materias { get; set; } = [];
    public Avaliacao? Avaliacao { get; set; }
}
