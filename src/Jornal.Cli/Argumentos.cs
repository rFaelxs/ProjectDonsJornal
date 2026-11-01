namespace Jornal.Cli;

/// <summary>
/// Leitura simples de argumentos. Um `switch` sobre `args` cobre o que estes comandos
/// precisam; uma biblioteca de linha de comando seria peso morto aqui.
/// </summary>
public class Argumentos(string[] args)
{
    private readonly string[] _args = args;

    /// <summary>True se a flag aparece, como em <c>--atualizar</c>.</summary>
    public bool TemFlag(string nome) =>
        _args.Any(a => a.Equals($"--{nome}", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Valor de uma opção, aceitando <c>--opcao valor</c> e <c>--opcao=valor</c>.
    /// </summary>
    public string? Valor(string nome)
    {
        for (var i = 0; i < _args.Length; i++)
        {
            var atual = _args[i];

            if (atual.StartsWith($"--{nome}=", StringComparison.OrdinalIgnoreCase))
                return atual[(nome.Length + 3)..];

            if (!atual.Equals($"--{nome}", StringComparison.OrdinalIgnoreCase)) continue;

            var proximo = i + 1 < _args.Length ? _args[i + 1] : null;
            return proximo is null || proximo.StartsWith("--") ? null : proximo;
        }

        return null;
    }

    /// <summary>O primeiro argumento que não é flag nem valor de opção.</summary>
    public string? Posicional(int indice = 0) =>
        _args.Skip(1).Where(a => !a.StartsWith("--")).Skip(indice).FirstOrDefault();
}
