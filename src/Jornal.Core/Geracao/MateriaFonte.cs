using Jornal.Core.Coleta;
using Jornal.Core.Comum;

namespace Jornal.Core.Geracao;

/// <summary>
/// Liga a matéria aos itens que a sustentam (RF23). É daqui que o renderizador monta o rodapé
/// de fontes — o LLM nunca escreve a fonte (RF31).
/// </summary>
public class MateriaFonte
{
    public Guid MateriaId { get; set; }
    public Materia? Materia { get; set; }

    public Guid ItemId { get; set; }
    public Item? Item { get; set; }

    public PapelFonte Papel { get; set; }

    /// <summary>
    /// Frase do redator sobre o relacionado. Só para <see cref="PapelFonte.Relacionado"/>.
    /// </summary>
    public string? Frase { get; set; }

    /// <summary>
    /// Ex.: "inscrições abertas" (RF22). Só pode existir se o texto do trecho afirma isso —
    /// o verificador confere.
    /// </summary>
    public string? StatusRelacionado { get; set; }
}
