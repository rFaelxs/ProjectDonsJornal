using Jornal.Core.Coleta;
using Jornal.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jornal.Infrastructure.Persistencia.Configuracoes;

public class ItemConfig : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.Fonte).HasMaxLength(120).IsRequired();
        b.Property(i => i.Titulo).HasMaxLength(500).IsRequired();

        // RN02 e CA-04.4: o resumo nunca passa de 600 caracteres.
        b.Property(i => i.Resumo).HasMaxLength(600).IsRequired();

        b.Property(i => i.Link).HasMaxLength(1000).IsRequired();

        // SHA-256 em hexadecimal. Primeira etapa da deduplicação (RF15, CA-04.5).
        b.Property(i => i.LinkHash).HasMaxLength(64).IsRequired().IsFixedLength();
        b.HasIndex(i => i.LinkHash).IsUnique();

        b.HasIndex(i => i.PublicadoEm);

        b.HasOne(i => i.Feed)
            .WithMany()
            .HasForeignKey(i => i.FeedId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(i => i.Categoria)
            .WithMany()
            .HasForeignKey(i => i.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Auto-relação: o item duplicado aponta para o original e fica guardado (RF16).
        b.HasOne(i => i.Original)
            .WithMany()
            .HasForeignKey(i => i.DuplicataDe)
            .OnDelete(DeleteBehavior.Restrict);

        // A indexação busca itens sem trechos e sem duplicata_de.
        b.HasIndex(i => i.DuplicataDe);
    }
}

public class TrechoConfig : IEntityTypeConfiguration<Trecho>
{
    public void Configure(EntityTypeBuilder<Trecho> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Texto).HasMaxLength(2000).IsRequired();
        b.Property(t => t.Fonte).HasMaxLength(120).IsRequired();

        b.Property(t => t.Embedding)
            .HasColumnType($"vector({JornalDbContext.DimensoesEmbedding})")
            .HasConversion<EmbeddingConverter, EmbeddingComparer>();

        // Índice vetorial da spec 01. HNSW com distância de cosseno, que é a métrica
        // usada em toda a recuperação (spec 05).
        b.HasIndex(t => t.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");

        // CA-01.2: a consulta de similaridade filtra por janela de 24h e por categoria.
        b.HasIndex(t => t.PublicadoEm);
        b.HasIndex(t => new { t.CategoriaId, t.PublicadoEm });

        b.HasIndex(t => new { t.ItemId, t.Ordem }).IsUnique();

        b.HasOne(t => t.Item)
            .WithMany(i => i.Trechos)
            .HasForeignKey(t => t.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(t => t.Categoria)
            .WithMany()
            .HasForeignKey(t => t.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ColetaFalhaConfig : IEntityTypeConfiguration<ColetaFalha>
{
    public void Configure(EntityTypeBuilder<ColetaFalha> b)
    {
        b.HasKey(f => f.Id);
        b.Property(f => f.Mensagem).HasMaxLength(2000).IsRequired();
        b.Property(f => f.Tipo).HasConversion<EnumSnakeCaseConverter<TipoColetaFalha>>().HasMaxLength(20);
        b.HasIndex(f => new { f.FeedId, f.OcorreuEm });

        b.HasOne(f => f.Feed)
            .WithMany()
            .HasForeignKey(f => f.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(f => f.Ciclo)
            .WithMany()
            .HasForeignKey(f => f.CicloId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
