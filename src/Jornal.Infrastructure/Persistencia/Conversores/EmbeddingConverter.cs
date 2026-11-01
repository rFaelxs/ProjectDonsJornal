using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

namespace Jornal.Infrastructure.Persistencia.Conversores;

/// <summary>
/// Converte o <c>float[]</c> do domínio para o <c>Pgvector.Vector</c> do provider.
/// Existe para que Jornal.Core não precise referenciar uma biblioteca específica de
/// PostgreSQL — a regra de dependência da spec 00.
/// </summary>
public class EmbeddingConverter() : ValueConverter<float[]?, Vector?>(
    dominio => dominio == null ? null : new Vector(dominio),
    provider => provider == null ? null : provider.Memory.ToArray());

/// <summary>
/// Sem isto o EF compara arrays por referência e perde alterações no embedding.
/// </summary>
public class EmbeddingComparer() : ValueComparer<float[]?>(
    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
    v => v == null ? 0 : v.Aggregate(17, (acc, f) => acc * 31 + f.GetHashCode()),
    v => v == null ? null : v.ToArray());

/// <summary>
/// Converte toda data para UTC antes de gravar. O Npgsql só aceita offset 0 em
/// <c>timestamptz</c>, e os feeds brasileiros mandam datas com <c>-03:00</c>. A spec 01 já
/// manda guardar em UTC e exibir em America/Sao_Paulo, então esta é a regra, não um remendo.
///
/// Fica como convenção global para não depender de cada ponto do código lembrar de chamar
/// ToUniversalTime().
/// </summary>
public class DataUtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
    dominio => dominio.ToUniversalTime(),
    provider => provider.ToUniversalTime());

/// <inheritdoc cref="DataUtcConverter"/>
public class DataUtcNullableConverter() : ValueConverter<DateTimeOffset?, DateTimeOffset?>(
    dominio => dominio == null ? null : dominio.Value.ToUniversalTime(),
    provider => provider == null ? null : provider.Value.ToUniversalTime());
