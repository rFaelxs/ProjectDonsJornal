using Jornal.Core.Recuperacao;

namespace Jornal.Core.Geracao;

public enum VereditoAfirmacao { Sustentada, NaoSustentada }

/// <summary>Uma afirmação factual extraída da matéria e o veredito do verificador.</summary>
public record Afirmacao(
    string Texto,
    VereditoAfirmacao Veredito,
    /// <summary>RefPrompt do trecho que sustenta. Nulo quando não sustentada.</summary>
    string? Trecho);

/// <summary>
/// Resultado da verificação (RN01). Reprovada na primeira vez, o redator tenta de novo com as
/// afirmações não sustentadas como "não diga isto"; reprovada de novo, a matéria é descartada.
/// </summary>
public record VerificacaoResultado(bool Aprovada, IReadOnlyList<Afirmacao> Afirmacoes)
{
    public IEnumerable<Afirmacao> NaoSustentadas =>
        Afirmacoes.Where(a => a.Veredito == VereditoAfirmacao.NaoSustentada);
}

/// <summary>Confere cada afirmação da matéria contra os trechos que a originaram (RN01, RN05).</summary>
public interface IVerificador
{
    Task<VerificacaoResultado> VerificarAsync(
        MateriaRascunho materia,
        IReadOnlyList<TrechoRecuperado> baseDeTrechos,
        CancellationToken ct);
}
