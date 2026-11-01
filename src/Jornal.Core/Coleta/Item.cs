using Jornal.Core.Catalogo;

namespace Jornal.Core.Coleta;

/// <summary>
/// Uma notícia coletada de um feed. Guarda só título e resumo (RN02) — nunca a matéria inteira.
/// O acervo nunca é apagado (RF16).
/// </summary>
public class Item
{
    public Guid Id { get; set; }

    public Guid FeedId { get; set; }
    public Feed? Feed { get; set; }

    /// <summary>Desnormalizado do feed, porque o feed pode trocar de categoria depois.</summary>
    public Guid CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    /// <summary>Nome do veículo, copiado do feed no momento da coleta.</summary>
    public required string Fonte { get; set; }

    public required string Titulo { get; set; }

    /// <summary>Texto puro, sem HTML, truncado em 600 caracteres no fim de frase (RN02, CA-04.4).</summary>
    public required string Resumo { get; set; }

    public required string Link { get; set; }

    /// <summary>SHA-256 do link canônico. Único — é a primeira etapa da deduplicação (RF15).</summary>
    public required string LinkHash { get; set; }

    public DateTimeOffset PublicadoEm { get; set; }

    /// <summary>Item sem data no feed recebe a hora da coleta e este flag (spec 04).</summary>
    public bool DataEstimada { get; set; }

    public DateTimeOffset ColetadoEm { get; set; }

    /// <summary>
    /// Preenchido quando o embedding ficou ≥ 0.93 de um item das últimas 72h (RF15).
    /// Item duplicado fica guardado, mas não gera trechos.
    /// </summary>
    public Guid? DuplicataDe { get; set; }
    public Item? Original { get; set; }

    public List<Trecho> Trechos { get; set; } = [];
}
