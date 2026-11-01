using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Jornal.Core.Perfis;

namespace Jornal.Core.Geracao;

/// <summary>
/// Um bloco da edição. Matérias reprovadas pelo verificador ficam gravadas com
/// <see cref="Verificada"/> = false e não entram no PDF (RN01).
/// </summary>
public class Materia
{
    public Guid Id { get; set; }

    public Guid EdicaoId { get; set; }
    public Edicao? Edicao { get; set; }

    /// <summary>Posição no documento.</summary>
    public int Ordem { get; set; }

    /// <summary>Nome da categoria, "Destaque" ou "Sugestões".</summary>
    public required string Secao { get; set; }

    public TipoMateria Tipo { get; set; }

    /// <summary>Preenchido quando a matéria veio de um tema específico ou auxiliar.</summary>
    public Guid? TemaPerfilId { get; set; }
    public TemaPerfil? TemaPerfil { get; set; }

    /// <summary>Preenchido quando a matéria veio de uma categoria escolhida.</summary>
    public Guid? CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    /// <summary>Até 70 caracteres. Em manchete dupla (RF27) contém exatamente um " + ".</summary>
    public required string Titulo { get; set; }

    /// <summary>Uma frase, até 140 caracteres, que explica o título.</summary>
    public string? LinhaFina { get; set; }

    /// <summary>Entre 150 e 450 caracteres. Vazio quando a profundidade é Breve.</summary>
    public string? Texto { get; set; }

    /// <summary>Só matérias verificadas aparecem no PDF (RN01).</summary>
    public bool Verificada { get; set; }

    /// <summary>"sem_base" ou "verificador" quando descartada. Nulo quando publicada.</summary>
    public string? MotivoDescarte { get; set; }

    public List<MateriaFonte> Fontes { get; set; } = [];
}
