using Jornal.Core.Comum;
using Jornal.Infrastructure;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Ponto de entrada do comando `jornal`. Cada subcomando entra no marco da sua spec;
// os que ainda não existem dizem isso em vez de falhar silenciosamente.

var host = Host.CreateApplicationBuilder(args);

// O content root fica no diretório de trabalho, para que caminhos como `data/edicoes` e
// `prompts/manchetes.v1.md` funcionem ao rodar da raiz do repo. Mas o appsettings.json vive
// ao lado do executável, então precisa ser carregado pelo caminho absoluto.
host.Configuration.AddJsonFile(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false, reloadOnChange: false);

host.Services.AddOptions<JornalOptions>()
    .Bind(host.Configuration.GetSection(JornalOptions.Secao))
    .ValidateOnStart();
host.Services.AddJornalInfrastructure(host.Configuration);

using var app = host.Build();

var comando = args.FirstOrDefault();

return comando switch
{
    "migrar" => await MigrarAsync(app.Services),
    "status" => await StatusAsync(app.Services),

    "seed" => NaoImplementado("seed", "M1", "spec 02"),
    "validar-feeds" => NaoImplementado("validar-feeds", "M1", "spec 02"),
    "coletar" => NaoImplementado("coletar", "M2", "spec 04"),
    "pauta" => NaoImplementado("pauta", "M3", "spec 05"),
    "calibrar" => NaoImplementado("calibrar", "M3", "spec 09"),
    "gerar" => NaoImplementado("gerar", "M5", "spec 07"),
    "comparar-rag" => NaoImplementado("comparar-rag", "M7", "spec 09"),
    "exportar-teste" => NaoImplementado("exportar-teste", "M6", "spec 08"),
    "excluir-usuario" => NaoImplementado("excluir-usuario", "M6", "spec 08"),

    null or "-h" or "--help" or "help" => Ajuda(),
    _ => Desconhecido(comando)
};

// Aplica as migrations pendentes. Equivale ao `dotnet ef database update`, mas sem
// exigir o SDK do EF instalado na máquina que roda o ciclo.
static async Task<int> MigrarAsync(IServiceProvider sp)
{
    using var escopo = sp.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<JornalDbContext>();

    var pendentes = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pendentes.Count == 0)
    {
        Console.WriteLine("Banco já está atualizado.");
        return 0;
    }

    Console.WriteLine($"Aplicando {pendentes.Count} migration(s): {string.Join(", ", pendentes)}");
    await db.Database.MigrateAsync();
    Console.WriteLine("Pronto.");
    return 0;
}

// Diagnóstico rápido: o banco responde, a extensão está lá e o acervo tem quanto.
static async Task<int> StatusAsync(IServiceProvider sp)
{
    using var escopo = sp.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<JornalDbContext>();

    if (!await db.Database.CanConnectAsync())
    {
        Console.Error.WriteLine("Não foi possível conectar ao banco. O docker compose está de pé?");
        return 1;
    }

    var pendentes = (await db.Database.GetPendingMigrationsAsync()).Count();
    Console.WriteLine($"Banco           : conectado");
    Console.WriteLine($"Migrations      : {(pendentes == 0 ? "em dia" : $"{pendentes} pendente(s)")}");
    Console.WriteLine($"Categorias      : {await db.Categorias.CountAsync()}");
    Console.WriteLine($"Feeds ativos    : {await db.Feeds.CountAsync(f => f.Ativo)}");
    Console.WriteLine($"Itens no acervo : {await db.Itens.CountAsync()}");
    Console.WriteLine($"Trechos         : {await db.Trechos.CountAsync()}");
    Console.WriteLine($"Edições prontas : {await db.Edicoes.CountAsync(e => e.Status == Jornal.Core.Geracao.StatusEdicao.Pronta)}");
    return 0;
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

        Disponíveis agora (M0):
          migrar                 aplica as migrations pendentes
          status                 mostra conexão, migrations e tamanho do acervo

        Próximos marcos:
          seed                   carrega data/catalogo.json no banco          (M1)
          validar-feeds          testa as URLs e liga os feeds aprovados      (M1)
          coletar                lê os feeds ativos e indexa                  (M2)
          pauta                  imprime a pauta da edição no terminal        (M3)
          calibrar --tema "..."  mostra os 20 trechos mais próximos           (M3)
          gerar [--data] [--sem-coleta]   roda o ciclo e gera o PDF           (M5)
          comparar-rag --tema "..."       com e sem RAG, lado a lado          (M7)
          exportar-teste --saida dados.csv                                    (M6)
          excluir-usuario <apelido>                                           (M6)
        """);
    return 0;
}

static int Desconhecido(string comando)
{
    Console.Error.WriteLine($"Comando desconhecido: '{comando}'. Use 'jornal --help'.");
    return 64;
}
