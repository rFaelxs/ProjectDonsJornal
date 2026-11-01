using FluentAssertions;
using Jornal.Core.Coleta;

namespace Jornal.Core.Tests;

/// <summary>Divisão em trechos (spec 04, RF14).</summary>
public class DivisorDeTrechosTests
{
    [Fact]
    public void Item_curto_vira_um_trecho_unico()
    {
        // O caso de quase 100% dos itens, porque só guardamos título e resumo (RN02).
        var trechos = DivisorDeTrechos.Dividir(
            "Banco Central mantém a Selic",
            "O Copom decidiu manter a taxa básica em 10,5% ao ano.");

        trechos.Should().HaveCount(1);
        trechos[0].Should().Be(
            "Banco Central mantém a Selic. O Copom decidiu manter a taxa básica em 10,5% ao ano.");
    }

    [Fact]
    public void Texto_base_nao_duplica_pontuacao_do_titulo()
    {
        var texto = DivisorDeTrechos.TextoBase("O dólar caiu?", "Fechou a R$ 5,10.");

        texto.Should().Be("O dólar caiu? Fechou a R$ 5,10.");
    }

    [Fact]
    public void Item_sem_resumo_usa_so_o_titulo()
    {
        var trechos = DivisorDeTrechos.Dividir("Título sozinho", "");

        trechos.Should().ContainSingle().Which.Should().Be("Título sozinho");
    }

    [Fact]
    public void Item_vazio_nao_gera_trecho()
    {
        DivisorDeTrechos.Dividir("", "").Should().BeEmpty();
    }

    [Fact]
    public void Texto_acima_do_limite_e_dividido_em_varias_janelas()
    {
        var frase = "Esta é uma frase de tamanho médio usada para encher o resumo do teste. ";
        var resumoLongo = string.Concat(Enumerable.Repeat(frase, 40)); // ~2.800 caracteres

        var trechos = DivisorDeTrechos.Dividir("Título do item longo", resumoLongo);

        trechos.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Todo_trecho_repete_o_titulo_para_nao_perder_contexto()
    {
        const string titulo = "Reforma tributária avança no Senado";
        var frase = "O relatório foi aprovado com destaques e segue para votação em plenário. ";
        var resumoLongo = string.Concat(Enumerable.Repeat(frase, 40));

        var trechos = DivisorDeTrechos.Dividir(titulo, resumoLongo);

        trechos.Should().OnlyContain(t => t.StartsWith(titulo, StringComparison.Ordinal),
            "a spec 04 manda repetir o título no começo de cada trecho");
    }

    [Fact]
    public void Janelas_consecutivas_compartilham_uma_frase()
    {
        var frases = Enumerable.Range(1, 40)
            .Select(i => $"Frase número {i} com algum conteúdo para ocupar espaço no resumo.");
        var resumoLongo = string.Join(' ', frases);

        var trechos = DivisorDeTrechos.Dividir("Título", resumoLongo);

        trechos.Should().HaveCountGreaterThan(1);

        // A última frase de um trecho reaparece no começo do corpo do seguinte.
        for (var i = 0; i < trechos.Count - 1; i++)
        {
            var ultimaFrase = UltimaFrase(trechos[i]);
            trechos[i + 1].Should().Contain(ultimaFrase,
                "a sobreposição de uma frase mantém o contexto na fronteira");
        }
    }

    [Fact]
    public void Nao_corta_frase_em_abreviacao_nem_em_valor()
    {
        // "R$ 1.500" e "Dr." têm ponto que não termina frase.
        var trechos = DivisorDeTrechos.Dividir(
            "Salário sobe", "O piso foi para R$ 1.500 segundo o Dr. Silva, do ministério.");

        trechos.Should().ContainSingle();
        trechos[0].Should().Contain("R$ 1.500").And.Contain("Dr. Silva");
    }

    private static string UltimaFrase(string trecho)
    {
        var fim = trecho.TrimEnd().LastIndexOfAny(['.', '!', '?']);
        var antes = trecho[..fim].LastIndexOfAny(['.', '!', '?']);
        return trecho[(antes + 1)..(fim + 1)].Trim();
    }
}
