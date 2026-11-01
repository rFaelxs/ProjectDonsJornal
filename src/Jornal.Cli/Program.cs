using Jornal.Cli;
using Jornal.Cli.Comandos;
using Jornal.Core.Comum;
using Jornal.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Ponto de entrada do comando `jornal`. Cada subcomando entra no marco da sua spec;
// os que ainda não existem dizem isso em vez de falhar silenciosamente.

var host = Host.CreateApplicationBuilder(args);

// O content root fica no diretório de trabalho, para que caminhos como `data/catalogo.json` e
// `prompts/manchetes.v1.md` funcionem ao rodar da raiz do repo. Mas o appsettings.json vive
// ao lado do executável, então precisa ser carregado pelo caminho absoluto.
host.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false, reloadOnChange: false);

// Num CLI o log de infraestrutura só atrapalha a leitura da saída. O que importa para o
// usuário o próprio comando imprime; use --verboso para ver o resto.
if (!args.Contains("--verboso"))
{
    host.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
    host.Logging.AddFilter("System.Net.Http", LogLevel.Warning);
    host.Logging.AddFilter("Polly", LogLevel.Warning);
    host.Logging.AddFilter("Jornal.Infrastructure", LogLevel.Warning);
}

host.Services.AddOptions<JornalOptions>()
    .Bind(host.Configuration.GetSection(JornalOptions.Secao))
    .ValidateOnStart();
host.Services.AddJornalInfrastructure(host.Configuration);

using var app = host.Build();

// Ctrl+C cancela o comando em vez de matar o processo no meio de uma transação.
using var cancelamento = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelamento.Cancel();
    Console.Error.WriteLine("\nCancelando...");
};

var argumentos = new Argumentos(args);
var ct = cancelamento.Token;

try
{
    return args.FirstOrDefault() switch
    {
        "migrar" => await ComandoBanco.MigrarAsync(app.Services, ct),
        "status" => await ComandoBanco.StatusAsync(app.Services, ct),
        "seed" => await ComandoSeed.ExecutarAsync(app.Services, argumentos, ct),
        "validar-feeds" => await ComandoValidarFeeds.ExecutarAsync(app.Services, argumentos, ct),
        "validar-url" => await ComandoValidarUrl.ExecutarAsync(app.Services, args, ct),

        "coletar" => await ComandoColetar.ExecutarAsync(app.Services, argumentos, ct),
        "pauta" => NaoImplementado("pauta", "M3", "spec 05"),
        "calibrar" => NaoImplementado("calibrar", "M3", "spec 09"),
        "gerar" => NaoImplementado("gerar", "M5", "spec 07"),
        "comparar-rag" => NaoImplementado("comparar-rag", "M7", "spec 09"),
        "exportar-teste" => NaoImplementado("exportar-teste", "M6", "spec 08"),
        "excluir-usuario" => NaoImplementado("excluir-usuario", "M6", "spec 08"),

        null or "-h" or "--help" or "help" => Ajuda(),
        var desconhecido => Desconhecido(desconhecido)
    };
}
catch (OperationCanceledException)
{
    return 130;
}

static int NaoImplementado(string comando, string marco, string spec)
{
    Console.Error.WriteLine($"'{comando}' entra no marco {marco} ({spec}). Ainda não implementado.");
    return 2;
}

static int Ajuda()
{
    Console.WriteLine("""
        jornal — Jornal Personalizado

        Banco:
          migrar                              aplica as migrations pendentes
          status                              conexão, migrations, acervo e RN08

        Catálogo e feeds (M1):
          seed [--arquivo <caminho>]          carrega o catalogo.json no banco
          validar-feeds [--categoria <slug>]  testa as URLs dos feeds
                        [--atualizar]         grava o resultado em feed.ativo
          validar-url <url> ...               valida URLs soltas, sem tocar no banco

        Acervo (M2):
          coletar [--sem-indexar]             lê os feeds ativos, normaliza e indexa

        Próximos marcos:
          pauta                               imprime a pauta da edição            (M3)
          calibrar --tema "..."               os 20 trechos mais próximos          (M3)
          gerar [--data] [--sem-coleta]       roda o ciclo e gera o PDF            (M5)
          comparar-rag --tema "..."           com e sem RAG, lado a lado           (M7)
          exportar-teste --saida dados.csv                                         (M6)
          excluir-usuario <apelido>                                                (M6)
        """);
    return 0;
}

static int Desconhecido(string comando)
{
    Console.Error.WriteLine($"Comando desconhecido: '{comando}'. Use 'jornal --help'.");
    return 64;
}
