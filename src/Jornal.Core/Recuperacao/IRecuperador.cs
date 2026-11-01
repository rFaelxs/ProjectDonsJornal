using Jornal.Core.Perfis;

namespace Jornal.Core.Recuperacao;

/// <summary>Monta a pauta da edição a partir do acervo e do perfil (spec 05).</summary>
public interface IRecuperador
{
    Task<PautaDaEdicao> MontarPautaAsync(Perfil perfil, DateTimeOffset agora, CancellationToken ct);
}
