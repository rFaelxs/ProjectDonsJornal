using Jornal.Core.Coleta;
using Jornal.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Jornal.Cli.Comandos;

/// <summary>
/// <c>jornal coletar [--sem-indexar]</c> — lê os feeds ativos e indexa (spec 04).
/// É este comando que o Agendador de Tarefas do Windows chama para acumular acervo a
/// partir do M2, sem esperar o ciclo completo do M5.
/// </summary>
public static class ComandoColetar
{
    public static async Task<int> ExecutarAsync(
        IServiceProvider sp, Argumentos args, CancellationToken ct)
    {
        using var escopo = sp.CreateScope();
        var coleta = escopo.ServiceProvider.GetRequiredService<IColetaService>();
        var indexador = escopo.ServiceProvider.GetRequiredService<IIndexador>();
        var db = escopo.ServiceProvider.GetRequiredService<JornalDbContext>();

        if (!await db.Database.CanConnectAsync(ct))
        {
            Console.Error.WriteLine("Não foi possível conectar ao banco.");
            Console.Error.WriteLine("Suba a infraestrutura: docker compose up -d postgres");
            return 69;
        }

        var ativos = await db.Feeds.CountAsync(f => f.Ativo, ct);
        if (ativos == 0)
        {
            Console.Error.WriteLine("Nenhum feed ativo no banco.");
            Console.Error.WriteLine("Rode 'jornal seed' e 'jornal validar-feeds --atualizar'.");
            return 1;
        }

        Console.WriteLine($"Lendo {ativos} feeds ativos...");
        var resumo = await coleta.ColetarAsync(ct);

        Console.WriteLine();
        Console.WriteLine($"  Feeds lidos        : {resumo.FeedsLidos}");
        Console.WriteLine($"  Feeds com falha    : {resumo.FeedsComFalha}");
        Console.WriteLine($"  Itens novos        : {resumo.ItensNovos}");
        Console.WriteLine($"  Ignorados por link : {resumo.ItensIgnoradosPorLink}");

        // RF17: só é falha de verdade quando nenhum feed respondeu.
        if (resumo.TodosFalharam)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Todos os feeds falharam. Confira a rede e a tabela coleta_falha.");
            return 1;
        }

        if (args.TemFlag("sem-indexar"))
        {
            Console.WriteLine("\nIndexação pulada por --sem-indexar.");
            return 0;
        }

        Console.WriteLine("\nIndexando (gera embeddings; pode demorar)...");

        int trechos;
        try
        {
            trechos = await indexador.IndexarPendentesAsync(ct);
        }
        catch (HttpRequestException e)
        {
            Console.Error.WriteLine($"\nNão foi possível falar com o Ollama: {e.Message}");
            Console.Error.WriteLine("Os itens ficaram gravados; rode 'jornal coletar' de novo");
            Console.Error.WriteLine("depois de subir o Ollama — a indexação é idempotente.");
            return 69;
        }

        Console.WriteLine($"  Trechos indexados  : {trechos}");

        var duplicatas = await db.Itens.CountAsync(i => i.DuplicataDe != null, ct);
        var acervo = await db.Itens.CountAsync(ct);
        Console.WriteLine($"\nAcervo: {acervo} itens ({duplicatas} duplicatas), " +
                          $"{await db.Trechos.CountAsync(ct)} trechos.");

        return 0;
    }
}
