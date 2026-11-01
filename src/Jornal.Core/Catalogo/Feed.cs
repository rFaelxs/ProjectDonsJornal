namespace Jornal.Core.Catalogo;

/// <summary>Uma fonte RSS/Atom de uma categoria. Nenhuma URL fica ativa sem passar pelo validador (RF18).</summary>
public class Feed
{
    public Guid Id { get; set; }
    public Guid CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public required string NomeVeiculo { get; set; }
    public required string Url { get; set; }

    /// <summary>Só a coleta lê feeds ativos. O validador liga e desliga este campo (RN08).</summary>
    public bool Ativo { get; set; }

    public DateTimeOffset? ValidadoEm { get; set; }
    public DateTimeOffset? UltimaColetaEm { get; set; }

    /// <summary>3 falhas consecutivas desativam o feed (spec 02).</summary>
    public int FalhasConsecutivas { get; set; }

    /// <summary>Cabeçalhos de cache guardados entre coletas (spec 04).</summary>
    public string? ETag { get; set; }
    public DateTimeOffset? UltimoModificadoEm { get; set; }

    public List<FeedValidacao> Validacoes { get; set; } = [];
}
