using System.Net;
using System.Text;

namespace Jornal.Integration.Tests.Apoio;

/// <summary>
/// Servidor HTTP mínimo em memória. A spec 02 pede que o validador seja testado com um
/// servidor fake (CA-02.2), para os testes não dependerem de rede nem de feeds reais.
/// </summary>
public sealed class ServidorHttpFake : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, Resposta> _rotas = [];
    private readonly CancellationTokenSource _parar = new();

    public string BaseUrl { get; }

    public ServidorHttpFake()
    {
        var porta = PortaLivre();
        BaseUrl = $"http://127.0.0.1:{porta}/";
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _ = Task.Run(AtenderAsync);
    }

    private record Resposta(int Status, string Corpo, string TipoConteudo, string? Location);

    /// <summary>Registra uma rota que devolve 200 com o corpo dado.</summary>
    public string Rota(string caminho, string corpo, string tipoConteudo = "application/rss+xml")
    {
        _rotas[$"/{caminho}"] = new Resposta(200, corpo, tipoConteudo, null);
        return BaseUrl + caminho;
    }

    /// <summary>Registra uma rota que devolve um status de erro.</summary>
    public string RotaComStatus(string caminho, int status)
    {
        _rotas[$"/{caminho}"] = new Resposta(status, string.Empty, "text/plain", null);
        return BaseUrl + caminho;
    }

    /// <summary>Registra um redirect, para exercitar o limite de 3 saltos do passo 1.</summary>
    public string RotaRedirect(string caminho, string destino, int status = 301)
    {
        _rotas[$"/{caminho}"] = new Resposta(status, string.Empty, "text/plain", destino);
        return BaseUrl + caminho;
    }

    private async Task AtenderAsync()
    {
        while (!_parar.IsCancellationRequested)
        {
            HttpListenerContext contexto;
            try
            {
                contexto = await _listener.GetContextAsync();
            }
            catch (Exception) when (_parar.IsCancellationRequested || !_listener.IsListening)
            {
                return;
            }

            var caminho = contexto.Request.Url?.AbsolutePath ?? "/";

            if (_rotas.TryGetValue(caminho, out var resposta))
            {
                contexto.Response.StatusCode = resposta.Status;
                contexto.Response.ContentType = resposta.TipoConteudo;

                if (resposta.Location is not null)
                    contexto.Response.Headers["Location"] = resposta.Location;

                if (resposta.Corpo.Length > 0)
                {
                    var bytes = Encoding.UTF8.GetBytes(resposta.Corpo);
                    contexto.Response.ContentLength64 = bytes.Length;
                    await contexto.Response.OutputStream.WriteAsync(bytes);
                }
            }
            else
            {
                contexto.Response.StatusCode = 404;
            }

            contexto.Response.Close();
        }
    }

    private static int PortaLivre()
    {
        using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var porta = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return porta;
    }

    public void Dispose()
    {
        _parar.Cancel();
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
        _parar.Dispose();
    }
}
