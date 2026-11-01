using Jornal.Core.Comum;
using Jornal.Core.Perfis;
using Jornal.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jornal.Infrastructure.Persistencia.Configuracoes;

public class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Apelido).HasMaxLength(60).IsRequired();

        // O apelido identifica o usuário na exportação do teste (spec 08).
        b.HasIndex(u => u.Apelido).IsUnique();

        b.HasOne(u => u.Perfil)
            .WithOne(p => p.Usuario)
            .HasForeignKey<Perfil>(p => p.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PerfilConfig : IEntityTypeConfiguration<Perfil>
{
    public void Configure(EntityTypeBuilder<Perfil> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.TextoLivreOriginal).HasMaxLength(4000);
        b.Property(p => p.Fuso).HasMaxLength(60).IsRequired();
        b.HasIndex(p => p.UsuarioId).IsUnique();
    }
}

public class PerfilCategoriaConfig : IEntityTypeConfiguration<PerfilCategoria>
{
    public void Configure(EntityTypeBuilder<PerfilCategoria> b)
    {
        b.HasKey(pc => new { pc.PerfilId, pc.CategoriaId });

        // uuid[]: subtemas são um recorte opcional dentro da categoria, nunca consultados
        // isoladamente, então não justificam tabela própria.
        b.Property(pc => pc.SubtemasIds).HasColumnType("uuid[]");

        // RN07 exige exatamente 4 linhas por perfil — contagem que o banco não checa.
        // A regra fica em PerfilService e é coberta por teste (CA-03.1).
        b.ToTable(t => t.HasCheckConstraint("ck_perfil_categoria_prioridade", "prioridade BETWEEN 1 AND 5"));

        b.HasOne(pc => pc.Perfil)
            .WithMany(p => p.Categorias)
            .HasForeignKey(pc => pc.PerfilId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(pc => pc.Categoria)
            .WithMany()
            .HasForeignKey(pc => pc.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TemaPerfilConfig : IEntityTypeConfiguration<TemaPerfil>
{
    public void Configure(EntityTypeBuilder<TemaPerfil> b)
    {
        b.HasKey(t => t.Id);
        b.Property(t => t.Texto).HasMaxLength(200).IsRequired();
        b.Property(t => t.Tipo).HasConversion<EnumSnakeCaseConverter<TipoTema>>().HasMaxLength(20);
        b.Property(t => t.Profundidade).HasConversion<EnumSnakeCaseConverter<Profundidade>>().HasMaxLength(20);

        b.Property(t => t.Embedding)
            .HasColumnType($"vector({JornalDbContext.DimensoesEmbedding})")
            .HasConversion<EmbeddingConverter, EmbeddingComparer>();

        b.ToTable(t => t.HasCheckConstraint("ck_tema_perfil_prioridade", "prioridade BETWEEN 1 AND 5"));

        // A recuperação percorre os temas ativos do perfil a cada ciclo.
        b.HasIndex(t => new { t.PerfilId, t.Ativo });

        b.HasOne(t => t.Perfil)
            .WithMany(p => p.Temas)
            .HasForeignKey(t => t.PerfilId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(t => t.Categoria)
            .WithMany()
            .HasForeignKey(t => t.CategoriaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class SugestaoAuxiliarConfig : IEntityTypeConfiguration<SugestaoAuxiliar>
{
    public void Configure(EntityTypeBuilder<SugestaoAuxiliar> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.RefTipo).HasConversion<EnumSnakeCaseConverter<RefTipoSugestao>>().HasMaxLength(20);
        b.Property(s => s.Resposta).HasConversion<EnumSnakeCaseConverter<RespostaSugestao>>().HasMaxLength(20);

        // CA-03.3: a mesma referência não é sugerida duas vezes para o mesmo perfil.
        b.HasIndex(s => new { s.PerfilId, s.RefTipo, s.RefId }).IsUnique();

        b.HasOne(s => s.Perfil)
            .WithMany(p => p.SugestoesAuxiliares)
            .HasForeignKey(s => s.PerfilId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
