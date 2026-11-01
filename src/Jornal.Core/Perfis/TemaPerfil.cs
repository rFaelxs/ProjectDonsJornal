using Jornal.Core.Catalogo;
using Jornal.Core.Comum;

namespace Jornal.Core.Perfis;

public enum TipoTema
{
    /// <summary>Entidade, evento ou assunto nomeado e restrito. Pode virar manchete. Prioridade inicial 5.</summary>
    Especifico,
    /// <summary>Sugerido por proximidade vetorial e aceito pelo usuário (RF10). Só alimenta as sugestões.</summary>
    Auxiliar
}

/// <summary>Um interesse nomeado do perfil, com embedding próprio (RF06).</summary>
public class TemaPerfil
{
    public Guid Id { get; set; }

    public Guid PerfilId { get; set; }
    public Perfil? Perfil { get; set; }

    public TipoTema Tipo { get; set; }

    public required string Texto { get; set; }

    /// <summary>Nulo quando o tema não cai em nenhuma das 4 categorias — aí recupera no acervo todo.</summary>
    public Guid? CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    /// <summary>1 a 5. Específico começa em 5, auxiliar em 1 (RF04).</summary>
    public int Prioridade { get; set; }

    public Profundidade Profundidade { get; set; } = Profundidade.Normal;

    /// <summary>Embedding de "{Texto} ({nome da categoria})" quando tem categoria (spec 03, passo 7).</summary>
    public float[]? Embedding { get; set; }

    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Número da primeira edição em que este tema vale (RN10). Um tema confirmado hoje não
    /// altera a edição que já foi gerada.
    /// </summary>
    public int ValidoAPartirDaEdicao { get; set; }
}
