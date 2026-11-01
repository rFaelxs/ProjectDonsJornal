namespace Jornal.Core.Ciclo;

/// <summary>O que aconteceu numa execução do ciclo (spec 07).</summary>
public record ResultadoCiclo(
    Guid CicloId,
    StatusCiclo Status,
    EtapaCiclo EtapaFinal,
    Guid? EdicaoId,
    int ItensColetados,
    int TrechosIndexados,
    /// <summary>Preenchido quando o status é Falhou. Ex.: "sem_material".</summary>
    string? Erro)
{
    public bool Sucesso => Status == StatusCiclo.Ok;
}

/// <summary>Coleta → indexação → recuperação → geração → PDF → entrega (RF33).</summary>
public interface ICicloDiario
{
    Task<ResultadoCiclo> ExecutarAsync(Guid usuarioId, CancellationToken ct);
}
