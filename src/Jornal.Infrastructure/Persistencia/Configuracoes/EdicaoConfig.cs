using System.Text.Json;
using Jornal.Core.Ciclo;
using Jornal.Core.Comum;
using Jornal.Core.Feedback;
using Jornal.Core.Geracao;
using Jornal.Infrastructure.Persistencia.Conversores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jornal.Infrastructure.Persistencia.Configuracoes;

public class EdicaoConfig : IEntityTypeConfiguration<Edicao>
{
    public void Configure(EntityTypeBuilder<Edicao> b)
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Status).HasConversion<EnumSnakeCaseConverter<StatusEdicao>>().HasMaxLength(20);
        b.Property(e => e.PdfCaminho).HasMaxLength(500);
        b.Property(e => e.PromptVersao).HasMaxLength(60);
        b.Property(e => e.ModeloChat).HasMaxLength(120);

        // CA-07.4: o agendador não gera duas edições no mesmo dia para o mesmo usuário,
        // mesmo reiniciando o serviço. A garantia é do banco, não do código.
        b.HasIndex(e => new { e.UsuarioId, e.DataRef }).IsUnique();
        b.HasIndex(e => new { e.UsuarioId, e.Numero }).IsUnique();

        b.HasOne(e => e.Usuario)
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MateriaConfig : IEntityTypeConfiguration<Materia>
{
    public void Configure(EntityTypeBuilder<Materia> b)
    {
        b.HasKey(m => m.Id);
        b.Property(m => m.Secao).HasMaxLength(120).IsRequired();
        b.Property(m => m.Tipo).HasConversion<EnumSnakeCaseConverter<TipoMateria>>().HasMaxLength(20);

        // Limites da validação local da spec 06.
        b.Property(m => m.Titulo).HasMaxLength(70).IsRequired();
        b.Property(m => m.LinhaFina).HasMaxLength(140);
        b.Property(m => m.Texto).HasMaxLength(450);
        b.Property(m => m.MotivoDescarte).HasMaxLength(60);

        b.HasIndex(m => new { m.EdicaoId, m.Ordem });

        b.HasOne(m => m.Edicao)
            .WithMany(e => e.Materias)
            .HasForeignKey(m => m.EdicaoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.TemaPerfil)
            .WithMany()
            .HasForeignKey(m => m.TemaPerfilId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(m => m.Categoria)
            .WithMany()
            .HasForeignKey(m => m.CategoriaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class MateriaFonteConfig : IEntityTypeConfiguration<MateriaFonte>
{
    public void Configure(EntityTypeBuilder<MateriaFonte> b)
    {
        // Um item pode entrar duas vezes na mesma matéria com papéis diferentes
        // (base hoje, relacionado no histórico), por isso o papel faz parte da chave.
        b.HasKey(f => new { f.MateriaId, f.ItemId, f.Papel });

        b.Property(f => f.Papel).HasConversion<EnumSnakeCaseConverter<PapelFonte>>().HasMaxLength(20);
        b.Property(f => f.Frase).HasMaxLength(300);
        b.Property(f => f.StatusRelacionado).HasMaxLength(120);

        b.HasOne(f => f.Materia)
            .WithMany(m => m.Fontes)
            .HasForeignKey(f => f.MateriaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(f => f.Item)
            .WithMany()
            .HasForeignKey(f => f.ItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AvaliacaoConfig : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Comentario).HasMaxLength(4000);
        b.Property(a => a.NovoTopico).HasMaxLength(200);
        b.ToTable(t => t.HasCheckConstraint("ck_avaliacao_nota", "nota BETWEEN 1 AND 5"));

        // jsonb: as chaves são nomes de seção, que variam por edição. Esquema fixo não serve.
        b.Property(a => a.SecoesAvaliadas)
            .HasColumnType("jsonb")
            .HasConversion(
                dominio => JsonSerializer.Serialize(dominio, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<Dictionary<string, AvaliacaoSecao>>(json, (JsonSerializerOptions?)null)
                        ?? new Dictionary<string, AvaliacaoSecao>(),
                new ValueComparer<Dictionary<string, AvaliacaoSecao>>(
                    (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
                    d => d.Aggregate(0, (acc, kv) => acc ^ HashCode.Combine(kv.Key, kv.Value)),
                    d => new Dictionary<string, AvaliacaoSecao>(d)));

        // Só existe uma avaliação por edição (spec 08).
        b.HasIndex(a => a.EdicaoId).IsUnique();

        b.HasOne(a => a.Edicao)
            .WithOne(e => e.Avaliacao)
            .HasForeignKey<Avaliacao>(a => a.EdicaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AjustePerfilConfig : IEntityTypeConfiguration<AjustePerfil>
{
    public void Configure(EntityTypeBuilder<AjustePerfil> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Tipo).HasConversion<EnumSnakeCaseConverter<TipoAjuste>>().HasMaxLength(30);
        b.Property(a => a.Status).HasConversion<EnumSnakeCaseConverter<StatusAjuste>>().HasMaxLength(20);
        b.Property(a => a.Payload).HasColumnType("jsonb").IsRequired();
        b.Property(a => a.Justificativa).HasMaxLength(500).IsRequired();

        b.HasIndex(a => new { a.AvaliacaoId, a.Status });

        b.HasOne(a => a.Avaliacao)
            .WithMany(av => av.Ajustes)
            .HasForeignKey(a => a.AvaliacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CicloExecucaoConfig : IEntityTypeConfiguration<CicloExecucao>
{
    public void Configure(EntityTypeBuilder<CicloExecucao> b)
    {
        b.HasKey(c => c.Id);
        b.Property(c => c.EtapaAtual).HasConversion<EnumSnakeCaseConverter<EtapaCiclo>>().HasMaxLength(20);
        b.Property(c => c.Status).HasConversion<EnumSnakeCaseConverter<StatusCiclo>>().HasMaxLength(20);
        b.Property(c => c.Erro).HasMaxLength(4000);

        b.HasIndex(c => new { c.UsuarioId, c.IniciadoEm });

        b.HasOne(c => c.Usuario)
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(c => c.Edicao)
            .WithMany()
            .HasForeignKey(c => c.EdicaoId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
