using Jornal.Core.Catalogo;

namespace Jornal.Core.Coleta;

/// <summary>Um item lido do feed, já normalizado, antes de virar <see cref="Item"/>.</summary>
public record ItemLido(
    string Titulo,
    string Resumo,
    string Link,
    string LinkHash,
    DateTimeOffset PublicadoEm,
    bool DataEstimada);

/// <summary>Resultado da leitura de um feed. Falha não lança — vira <see cref="Falha"/>.</summary>
public record FeedReadResult(
    Guid FeedId,
    IReadOnlyList<ItemLido> Itens,
    int? HttpStatus,
    /// <summary>Novo ETag devolvido pelo servidor, para a próxima coleta.</summary>
    string? ETag,
    DateTimeOffset? UltimoModificadoEm,
    /// <summary>True quando o servidor respondeu 304 e não há nada novo.</summary>
    bool NaoModificado,
    (TipoColetaFalha Tipo, string Mensagem)? Falha)
{
    public bool Sucesso => Falha is null;
}

public interface IFeedReader
{
    Task<FeedReadResult> ReadAsync(Feed feed, CancellationToken ct);
}
