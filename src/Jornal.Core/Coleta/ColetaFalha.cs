using Jornal.Core.Catalogo;
using Jornal.Core.Ciclo;

namespace Jornal.Core.Coleta;

public enum TipoColetaFalha { Timeout, Http, Parse, Outro }

/// <summary>
/// Falha de um feed numa coleta (RF17). A coleta continua com os outros feeds; o ciclo só
/// aborta se todos falharem.
/// </summary>
public class ColetaFalha
{
    public Guid Id { get; set; }

    public Guid FeedId { get; set; }
    public Feed? Feed { get; set; }

    public Guid? CicloId { get; set; }
    public CicloExecucao? Ciclo { get; set; }

    public DateTimeOffset OcorreuEm { get; set; }
    public TipoColetaFalha Tipo { get; set; }
    public required string Mensagem { get; set; }
}
