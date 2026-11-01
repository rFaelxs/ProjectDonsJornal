using Jornal.Core.Recuperacao;

namespace Jornal.Core.Geracao;

/// <summary>Um relacionado conforme o redator o descreveu (RF21, RF22).</summary>
public record RelacionadoRascunho(
    /// <summary>RefPrompt do trecho, ex.: "t_301".</summary>
    string Ref,
    string Frase,
    /// <summary>Ex.: "inscrições abertas". Só se o trecho afirma — o verificador confere.</summary>
    string? Status);

/// <summary>
/// A saída do redator para um assunto, antes do verificador. Espelha o JSON estruturado
/// da spec 06.
/// </summary>
public record MateriaRascunho(
    string Titulo,
    string LinhaFina,
    string Texto,
    /// <summary>RefPrompt dos trechos citados. Não pode ser vazio e precisa ser subconjunto dos fornecidos.</summary>
    IReadOnlyList<string> FontesUsadas,
    IReadOnlyList<RelacionadoRascunho> Relacionados,
    /// <summary>O LLM declara que os trechos não sustentam uma matéria (RN01).</summary>
    bool SemBase)
{
    /// <summary>O assunto que originou este rascunho. Fora do JSON do LLM.</summary>
    public Assunto? Assunto { get; init; }
}

/// <summary>A edição antes de virar PDF: matérias aprovadas e descartadas, com o motivo.</summary>
public record EdicaoRascunho(
    Guid PerfilId,
    int PerfilVersao,
    DateTimeOffset Referencia,
    IReadOnlyList<MateriaRascunho> Aprovadas,
    IReadOnlyList<(MateriaRascunho Materia, string Motivo)> Descartadas,
    string PromptVersao,
    string ModeloChat);

/// <summary>Redige uma matéria por assunto, usando SOMENTE os trechos da pauta (RN02).</summary>
public interface IRedator
{
    Task<EdicaoRascunho> RedigirAsync(PautaDaEdicao pauta, CancellationToken ct);
}
