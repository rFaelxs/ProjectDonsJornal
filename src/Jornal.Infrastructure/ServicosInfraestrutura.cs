using System.Net;
using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Jornal.Infrastructure.Catalogo;
using Jornal.Infrastructure.Feeds;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;

namespace Jornal.Infrastructure;

/// <summary>
/// Composição da infraestrutura. Web e Cli chamam isto; nenhum dos dois conhece
/// EF Core, Npgsql, pgvector ou o cliente do Ollama diretamente.
/// </summary>
public static class ServicosInfraestrutura
{
    public static IServiceCollection AddJornalInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var conexao = configuration.GetConnectionString("Jornal")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Jornal não configurada. Veja o appsettings.json.");

        services.AddDbContext<JornalDbContext>(options => options
            .UseNpgsql(conexao, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention());

        var opcoes = configuration.GetSection(JornalOptions.Secao).Get<JornalOptions>()
                     ?? new JornalOptions();

        services.AddJornalFeeds(opcoes);
        services.AddJornalIa(opcoes);

        services.AddScoped<ISeedCatalogo, SeedCatalogoService>();
        services.AddScoped<IValidacaoFeeds, ValidacaoFeedsService>();

        return services;
    }

    /// <summary>
    /// Cliente HTTP dos feeds: timeout no handler, no máximo 3 redirects e um User-Agent
    /// identificável, como a spec 02 exige no passo 1.
    /// </summary>
    private static void AddJornalFeeds(this IServiceCollection services, JornalOptions opcoes)
    {
        services.AddHttpClient<IFeedValidator, FeedValidator>(FeedValidator.ClienteHttp, cliente =>
            {
                cliente.Timeout = TimeSpan.FromSeconds(opcoes.Coleta.TimeoutSegundos);
                cliente.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "JornalPersonalizado/0.1 (+projeto academico; leitor de RSS)");
                cliente.DefaultRequestHeaders.Accept.ParseAdd(
                    "application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9, */*;q=0.5");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            // 1 tentativa extra, como a spec 04 pede para a coleta.
            .AddStandardResilienceHandler(resiliencia =>
            {
                resiliencia.Retry.MaxRetryAttempts = 1;
                resiliencia.AttemptTimeout.Timeout =
                    TimeSpan.FromSeconds(opcoes.Coleta.TimeoutSegundos);
                resiliencia.TotalRequestTimeout.Timeout =
                    TimeSpan.FromSeconds(opcoes.Coleta.TimeoutSegundos * 3);
                resiliencia.CircuitBreaker.SamplingDuration =
                    TimeSpan.FromSeconds(opcoes.Coleta.TimeoutSegundos * 6);
            });
    }

    /// <summary>
    /// Embeddings são sempre locais (trocar o modelo exige reindexar o acervo). O chat é
    /// configurável, para trocar de provider sem tocar no resto (spec 00).
    /// </summary>
    private static void AddJornalIa(this IServiceCollection services, JornalOptions opcoes)
    {
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            new OllamaApiClient(new Uri(opcoes.Ai.Embedding.Endpoint), opcoes.Ai.Embedding.Model));

        services.AddSingleton<IChatClient>(_ => opcoes.Ai.Chat.Provider switch
        {
            "Ollama" => new OllamaApiClient(
                new Uri(opcoes.Ai.Chat.Endpoint), opcoes.Ai.Chat.Model),

            // OpenAICompatible entra no M7, junto com o plano B da demo (spec 09).
            _ => throw new NotSupportedException(
                $"Provider de chat '{opcoes.Ai.Chat.Provider}' ainda não implementado. " +
                "Use 'Ollama' ou aguarde o marco M7.")
        });
    }
}
