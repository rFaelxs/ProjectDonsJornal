using Jornal.Core.Comum;

namespace Jornal.Core.Recuperacao;

/// <summary>Como a matéria principal sai no documento (RF27).</summary>
public enum FormatoMateria
{
    Simples,
    /// <summary>Manchete dupla "A + B": dois assuntos redigidos juntos.</summary>
    Duplo
}

/// <summary>
/// A saída da recuperação: a lista ordenada de assuntos que vão virar matérias.
/// O LLM não escolhe o que entra no jornal, só redige — toda decisão de pauta é
/// determinística e testável (spec 05).
/// </summary>
public record PautaDaEdicao(
    Guid PerfilId,
    int PerfilVersao,
    DateTimeOffset Referencia,
    IReadOnlyList<Assunto> Assuntos,
    IReadOnlyList<Assunto> Sugestoes,
    IReadOnlyList<TemaSemMateria> Omitidos)
{
    /// <summary>Total de matérias, sem contar as sugestões. Limitado por Edicao:MaxMaterias (CA-05.6).</summary>
    public int TotalMaterias => Assuntos.Count;
}

/// <summary>
/// Um grupo de itens que tratam do mesmo fato em fontes diferentes (RF20). Vira uma matéria.
/// </summary>
public record Assunto(
    string Secao,
    TipoMateria Tipo,
    Guid? TemaPerfilId,
    Guid? CategoriaId,
    double Relevancia,
    int Prioridade,
    Profundidade Profundidade,
    IReadOnlyList<TrechoRecuperado> Base,
    IReadOnlyList<TrechoRecuperado> Relacionados)
{
    /// <summary>prioridade_do_tema × relevancia (spec 05, passo 3).</summary>
    public double Score => Prioridade * Relevancia;

    public FormatoMateria Formato { get; init; } = FormatoMateria.Simples;

    /// <summary>
    /// O segundo assunto da manchete dupla (RF27). Preenchido só quando
    /// <see cref="Formato"/> é <see cref="FormatoMateria.Duplo"/>; ele não gera matéria própria.
    /// </summary>
    public Assunto? Par { get; init; }

    /// <summary>Fontes distintas no assunto. Máximo 4 (spec 05, passo 2).</summary>
    public int NumeroDeFontes => Base.Select(t => t.Fonte).Distinct().Count();
}

/// <summary>Um trecho do acervo recuperado para a pauta, com a similaridade que o trouxe.</summary>
public record TrechoRecuperado(
    Guid TrechoId,
    Guid ItemId,
    /// <summary>Identificador curto usado nos prompts, ex.: "t_812" (spec 06).</summary>
    string RefPrompt,
    string Texto,
    string Fonte,
    string Link,
    DateTimeOffset PublicadoEm,
    double Similaridade);

/// <summary>
/// Tema ativo do perfil que não achou material. Não ganha seção e nada é completado com
/// conteúdo de fora do acervo (RN03).
/// </summary>
public record TemaSemMateria(
    Guid? TemaPerfilId,
    Guid? CategoriaId,
    string Texto,
    /// <summary>Código do motivo, ex.: "abaixo_do_limiar".</summary>
    string Motivo,
    /// <summary>A melhor similaridade encontrada, para a calibração do M3.</summary>
    double MelhorSimilaridade);
