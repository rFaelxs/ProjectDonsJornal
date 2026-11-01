namespace Jornal.Core.Perfis;

/// <summary>
/// Usuário pseudonimizado (RNF06). No MVP só existe o apelido — sem nome, e-mail ou
/// qualquer dado pessoal.
/// </summary>
public class Usuario
{
    public Guid Id { get; set; }

    /// <summary>Pseudônimo escolhido no onboarding. É por ele que a exportação identifica o usuário.</summary>
    public required string Apelido { get; set; }

    /// <summary>Aceite do termo, primeiro passo obrigatório do onboarding (RNF06).</summary>
    public DateTimeOffset ConsentimentoEm { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public Perfil? Perfil { get; set; }
}
