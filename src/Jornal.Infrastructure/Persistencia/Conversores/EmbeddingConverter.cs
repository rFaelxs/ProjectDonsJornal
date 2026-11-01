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
