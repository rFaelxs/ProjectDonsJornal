using Jornal.Core.Catalogo;

namespace Jornal.Core.Perfis;

/// <summary>Uma das 4 categorias escolhidas (RF07, RN07).</summary>
public class PerfilCategoria
{
    public Guid PerfilId { get; set; }
    public Perfil? Perfil { get; set; }

    public Guid CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    /// <summary>
    /// Subtemas marcados dentro da categoria, opcional. Vazio significa "a categoria toda";
    /// com subtemas, a recuperação faz uma consulta por subtema (spec 05).
    /// </summary>
    public Guid[] SubtemasIds { get; set; } = [];

    /// <summary>1 a 5. Começa em 3 e o feedback ajusta (RF04).</summary>
    public int Prioridade { get; set; } = 3;
}
