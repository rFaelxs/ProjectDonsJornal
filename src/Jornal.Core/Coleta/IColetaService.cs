namespace Jornal.Core.Coleta;

/// <summary>Números de uma coleta, usados pelo log do ciclo e pelo CLI.</summary>
public record ColetaResumo(
    int FeedsLidos,
    int FeedsComFalha,
    int ItensNovos,
    int ItensIgnoradosPorLink,
    int ItensMarcadosDuplicata)
{
    /// <summary>O ciclo só aborta se todos os feeds falharem (RF17).</summary>
    public bool TodosFalharam => FeedsLidos > 0 && FeedsComFalha == FeedsLidos;
}

/// <summary>Lê todos os feeds ativos e grava os itens novos (RF13).</summary>
public interface IColetaService
{
    Task<ColetaResumo> ColetarAsync(CancellationToken ct);
}

/// <summary>
/// Gera embeddings e trechos dos itens pendentes (RF14). Idempotente: processa itens sem
/// trechos e sem duplicata_de.
/// </summary>
public interface IIndexador
{
    Task<int> IndexarPendentesAsync(CancellationToken ct);
}
