namespace Jornal.Core.Perfis;

/// <summary>
/// Os interesses de um usuário. Toda alteração confirmada incrementa <see cref="Versao"/> e só
/// vale para a próxima edição (RN10).
/// </summary>
public class Perfil
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>O que o usuário digitou no passo 3 do onboarding (RF01). Guardado como veio.</summary>
    public string? TextoLivreOriginal { get; set; }

    /// <summary>Hora em que o ciclo começa (RF05). Padrão 06:30.</summary>
    public TimeOnly HorarioGeracao { get; set; } = new(6, 30);

    /// <summary>IANA, ex.: "America/Sao_Paulo".</summary>
    public string Fuso { get; set; } = "America/Sao_Paulo";

    /// <summary>Começa em 1 e sobe a cada lote de ajustes confirmado (spec 08).</summary>
    public int Versao { get; set; } = 1;

    /// <summary>Exatamente 4 linhas, validado no serviço (RN07).</summary>
    public List<PerfilCategoria> Categorias { get; set; } = [];

    public List<TemaPerfil> Temas { get; set; } = [];
    public List<SugestaoAuxiliar> SugestoesAuxiliares { get; set; } = [];
}
