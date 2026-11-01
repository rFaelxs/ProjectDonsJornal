namespace Jornal.Core.Catalogo;

/// <summary>Formato detectado pelo validador.</summary>
public enum FormatoFeed { Rss, Atom, Invalido }

/// <summary>Uma execução do validador sobre um feed (RF18). Fica como histórico.</summary>
public class FeedValidacao
{
    public Guid Id { get; set; }
    public Guid FeedId { get; set; }
    public Feed? Feed { get; set; }

    public DateTimeOffset ExecutadoEm { get; set; }
    public int? HttpStatus { get; set; }
    public FormatoFeed Formato { get; set; }

    /// <summary>Itens com data nos últimos 7 dias. O validador exige ao menos 3.</summary>
    public int ItensRecentes { get; set; }

    public bool Aprovado { get; set; }

    /// <summary>
    /// Código da reprovação: http_&lt;status&gt;, timeout, formato_invalido, sem_itens_recentes
    /// ou campos_incompletos. Nulo quando aprovado.
    /// </summary>
    public string? Motivo { get; set; }
}
