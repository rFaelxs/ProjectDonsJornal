namespace Jornal.Core.Geracao;

/// <summary>Gera o PDF A4 e a prévia em PNG da página 1 (spec 07).</summary>
public interface IEdicaoRenderer
{
    Task<byte[]> RenderPdfAsync(Edicao edicao, CancellationToken ct);

    /// <summary>PNG da página 1, usado na página web e no plano B da demo.</summary>
    Task<byte[]> RenderPreviaAsync(Edicao edicao, CancellationToken ct);

    /// <summary>Quantas páginas o PDF ocupa, para o corte de RNF05 sem gravar arquivo.</summary>
    Task<int> ContarPaginasAsync(Edicao edicao, CancellationToken ct);
}

/// <summary>
/// Entrega a edição pronta. No MVP há duas implementações ativas: pasta local e página web.
/// E-mail e impressão automática entram como novas implementações da mesma interface (RF35).
/// </summary>
public interface IEntregaEdicao
{
    Task EntregarAsync(Edicao edicao, byte[] pdf, CancellationToken ct);
}
