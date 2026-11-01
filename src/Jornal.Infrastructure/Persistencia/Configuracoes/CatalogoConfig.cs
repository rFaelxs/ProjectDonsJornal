using Jornal.Core.Catalogo;
using Jornal.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jornal.Infrastructure.Persistencia.Configuracoes;

public class CategoriaConfig : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.Slug).HasMaxLength(60).IsRequired();
        b.HasIndex(c => c.Slug).IsUnique();
        b.Property(c => c.Nome).HasMaxLength(120).IsRequired();
        b.Property(c => c.Descricao).HasMaxLength(1000).IsRequired();
        b.Property(c => c.EmbeddingHash).HasMaxLength(64);

        b.Property(c => c.Embedding)
            .HasColumnType($"vector({JornalDbContext.DimensoesEmbedding})")
            .HasConversion<EmbeddingConverter, EmbeddingComparer>();

        b.HasMany(c => c.Subtemas)
            .WithOne(s => s.Categoria)
            .HasForeignKey(s => s.CategoriaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SubtemaConfig : IEntityTypeConfiguration<Subtema>
{
    public void Configure(EntityTypeBuilder<Subtema> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Slug).HasMaxLength(60).IsRequired();
        b.HasIndex(s => new { s.CategoriaId, s.Slug }).IsUnique();
        b.Property(s => s.Nome).HasMaxLength(120).IsRequired();
        b.Property(s => s.Descricao).HasMaxLength(1000).IsRequired();
        b.Property(s => s.EmbeddingHash).HasMaxLength(64);

        b.Property(s => s.Embedding)
            .HasColumnType($"vector({JornalDbContext.DimensoesEmbedding})")
            .HasConversion<EmbeddingConverter, EmbeddingComparer>();
    }
}

public class FeedConfig : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> b)
    {
        b.HasKey(f => f.Id);
        b.Property(f => f.NomeVeiculo).HasMaxLength(120).IsRequired();
        b.Property(f => f.Url).HasMaxLength(500).IsRequired();
        b.HasIndex(f => f.Url).IsUnique();
        b.Property(f => f.ETag).HasMaxLength(200);

        // A coleta sempre busca "feeds ativos", então o índice cobre o filtro real.
        b.HasIndex(f => new { f.Ativo, f.CategoriaId });

        b.HasOne(f => f.Categoria)
            .WithMany(c => c.Feeds)
            .HasForeignKey(f => f.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class FeedValidacaoConfig : IEntityTypeConfiguration<FeedValidacao>
{
    public void Configure(EntityTypeBuilder<FeedValidacao> b)
    {
        b.HasKey(v => v.Id);
        b.Property(v => v.Motivo).HasMaxLength(60);
        b.Property(v => v.Formato).HasConversion<EnumSnakeCaseConverter<FormatoFeed>>().HasMaxLength(20);
        b.HasIndex(v => new { v.FeedId, v.ExecutadoEm });

        b.HasOne(v => v.Feed)
            .WithMany(f => f.Validacoes)
            .HasForeignKey(v => v.FeedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
