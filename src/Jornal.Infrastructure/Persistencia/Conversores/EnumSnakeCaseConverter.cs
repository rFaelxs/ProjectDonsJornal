using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jornal.Infrastructure.Persistencia.Conversores;

/// <summary>
/// Grava enums como texto em snake_case, exatamente com os valores escritos na spec 01
/// (<c>especifico</c>, <c>nao_sustentada</c>, <c>formato_invalido</c>). Texto legível no banco
/// vale mais que o int aqui: as consultas de diagnóstico e o CSV do teste de usuário ficam
/// compreensíveis sem consultar o código.
/// </summary>
public class EnumSnakeCaseConverter<TEnum>() : ValueConverter<TEnum, string>(
    valor => ParaSnakeCase(valor!.ToString()!),
    texto => ParaEnum(texto))
    where TEnum : struct, Enum
{
    private static string ParaSnakeCase(string nome) =>
        Regex.Replace(nome, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    private static TEnum ParaEnum(string texto)
    {
        var nome = string.Concat(texto.Split('_')
            .Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
        return Enum.Parse<TEnum>(nome);
    }
}
