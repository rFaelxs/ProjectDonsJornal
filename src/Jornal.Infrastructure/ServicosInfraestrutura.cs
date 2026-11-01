using System.Net;
using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Jornal.Core.Coleta;
using Jornal.Infrastructure.Catalogo;
using Jornal.Infrastructure.Coleta;
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
            // SplitQuery: o seed carrega categoria com subtemas e feeds de uma vez. Numa
            // consulta só isso vira produto cartesiano, e o EF avisa sobre o custo.
            .UseNpgsql(conexao, npgsql => npgsql
                .UseVector()
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .UseSnakeCaseNamingConvention());

        var opcoes = configuration.GetSection(JornalOptions.Secao).Get<JornalOptions>()
                     ?? new JornalOptions();

        services.AddJornalFeeds(opcoes);
        services.AddJornalIa(opcoes);

        services.AddScoped<ISeedCatalogo, SeedCatalogoService>();
        services.AddScoped<IValidacaoFeeds, ValidacaoFeedsService>();
        services.AddScoped<IColetaService, ColetaService>();
        services.AddScoped<IIndexador, Indexador>();

        return services;
    }

    /// <summary>
    /// Cliente HTTP dos feeds: timeout no handler, no máximo 3 redirects e um User-Agent
    /// identificável, como a spec 02 exige no passo 1.
    /// </summary>
    private static void AddJornalFeeds(this IServiceCollection services, JornalOptions opcoes)
    {
        services.AddHttpClient<IFeedValidator, FeedValidator>(FeedValidator.ClienteHttp,
                cliente => ConfigurarClienteDeFeeds(cliente, opcoes))
            .ConfigurePrimaryHttpMessageHandler(HandlerDeFeeds)
            .AddStandardResilienceHandler(r => ConfigurarResiliencia(r, opcoes));

        // O leitor da coleta compartilha a configuração do validador, mas é um cliente
        // próprio: o circuit breaker de um não deve derrubar o outro.
        services.AddHttpClient<IFeedReader, LeitorDeFeed>("coleta",
                cliente => ConfigurarClienteDeFeeds(cliente, opcoes))
            .ConfigurePrimaryHttpMessageHandler(HandlerDeFeeds)
            .AddStandardResilienceHandler(r => ConfigurarResiliencia(r, opcoes));
    }

    private static void ConfigurarClienteDeFeeds(HttpClient cliente, JornalOptions opcoes)
    {
        cliente.Timeout = TimeSpan.FromSeconds(opcoes.Coleta.TimeoutSegundos);
        cliente.DefaultRequestHeaders.UserAgent.ParseAdd(
            "JornalPersonalizado/0.1 (+projeto academico; leitor de RSS)");
        cliente.DefaultRequestHeaders.Accept.ParseAdd(
            "application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9, */*;q=0.5");
    }

    private static HttpClientHandler HandlerDeFeeds() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };

    /// <summary>1 tentativa extra, como a spec 04 pede para a coleta.</summary>
    private static void ConfigurarResiliencia(
        Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions resiliencia,
        JornalOptions opcoes)
    {
        var timeout = opcoes.Coleta.TimeoutSegundos;
        resiliencia.Retry.MaxRetryAttempts = 1;
        resiliencia.AttemptTimeout.Timeout = TimeSpan.FromSeconds(timeout);
        resiliencia.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(timeout * 3);
        resiliencia.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(timeout * 6);
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
