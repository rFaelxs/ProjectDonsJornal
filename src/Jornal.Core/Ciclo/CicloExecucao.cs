using Jornal.Core.Geracao;
using Jornal.Core.Perfis;

namespace Jornal.Core.Ciclo;

/// <summary>Etapas do ciclo diário, na ordem da spec 07.</summary>
public enum EtapaCiclo
{
    Coleta,
    Indexacao,
    Recuperacao,
    Geracao,
    Renderizacao,
    Persistencia,
    Entrega
}

public enum StatusCiclo { Executando, Ok, Falhou }

/// <summary>
/// Log de uma execução do ciclo diário. Guarda a etapa onde parou, para o diagnóstico da
/// falha (RN09) e para os números do teste de usuário.
/// </summary>
public class CicloExecucao
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTimeOffset IniciadoEm { get; set; }
    public DateTimeOffset? TerminadoEm { get; set; }

    public EtapaCiclo EtapaAtual { get; set; }
    public StatusCiclo Status { get; set; } = StatusCiclo.Executando;

    /// <summary>Exceção e etapa, quando falhou.</summary>
    public string? Erro { get; set; }

    public int ItensColetados { get; set; }
    public int TrechosIndexados { get; set; }

    public Guid? EdicaoId { get; set; }
    public Edicao? Edicao { get; set; }
}
