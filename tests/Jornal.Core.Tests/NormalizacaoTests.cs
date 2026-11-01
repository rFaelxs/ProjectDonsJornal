using FluentAssertions;
using Jornal.Core.Coleta;

namespace Jornal.Core.Tests;

/// <summary>
/// Critérios de aceite da normalização (spec 04). São funções puras, então rodam sem
/// banco, sem rede e sem LLM.
/// </summary>
public class NormalizacaoTests
{
    // CA-04.4 — nenhum resumo passa de 600 caracteres nem contém tags HTML.

    [Fact]
    public void CA_04_4_resumo_nunca_passa_de_600_caracteres()
    {
        var longo = string.Join(' ', Enumerable.Repeat("palavra", 400));

        var resumo = NormalizadorItem.Resumo(longo);

        resumo.Length.Should().BeLessThanOrEqualTo(NormalizadorItem.LimiteResumo);
    }

    [Fact]
    public void CA_04_4_resumo_nao_contem_tag_html()
    {
        const string bruto = """
            <p>A <strong>Receita Federal</strong> abriu consulta ao lote.</p>
            <div class="ads"><script>rastreia()</script></div>
            <a href="https://exemplo.test">Leia mais</a>
            """;

        var resumo = NormalizadorItem.Resumo(bruto);

        resumo.Should().NotContain("<").And.NotContain(">");
        resumo.Should().Contain("Receita Federal");
        resumo.Should().NotContain("rastreia");
    }

    [Fact]
    public void CA_04_4_resumo_decodifica_entidades_html()
    {
        var resumo = NormalizadorItem.Resumo(
            "Infla&ccedil;&atilde;o fica em 0,3% &amp; juros seguem &quot;est&aacute;veis&quot;");

        resumo.Should().Be("Inflação fica em 0,3% & juros seguem \"estáveis\"");
    }

    [Fact]
    public void CA_04_4_entidade_escapada_duas_vezes_nao_vira_tag()
    {
        // Um feed mal gerado pode mandar "&amp;lt;script&amp;gt;". Depois de duas
        // decodificações isso viraria tag se não removêssemos de novo.
        var resumo = NormalizadorItem.Resumo("Texto &amp;lt;script&amp;gt;alerta()&amp;lt;/script&amp;gt; fim");

        resumo.Should().NotContain("<").And.NotContain(">");
    }

    [Fact]
    public void Resumo_corta_em_fim_de_frase_quando_possivel()
    {
        var primeira = new string('a', 450) + ".";
        var segunda = " " + new string('b', 300) + ".";

        var resumo = NormalizadorItem.Resumo(primeira + segunda);

        resumo.Should().EndWith(".");
        resumo.Should().Be(primeira, "o corte respeita o fim da primeira frase");
    }

    [Fact]
    public void Resumo_de_texto_curto_fica_intacto()
    {
        const string texto = "Banco Central mantém a Selic em 10,5% ao ano.";

        NormalizadorItem.Resumo(texto).Should().Be(texto);
    }

    // CA-04.5 — link com utm_* e fragmento gera o mesmo hash do link limpo.

    [Fact]
    public void CA_04_5_utm_e_fragmento_nao_mudam_o_hash()
    {
        const string limpo = "https://exemplo.test/noticia/banco-central";
        const string sujo = "https://exemplo.test/noticia/banco-central?utm_source=x#topo";

        NormalizadorItem.HashDoLink(sujo).Should().Be(NormalizadorItem.HashDoLink(limpo));
    }

    [Theory]
    [InlineData("https://exemplo.test/a?utm_source=news&utm_medium=email")]
    [InlineData("https://exemplo.test/a?fbclid=abc123")]
    [InlineData("https://exemplo.test/a#comentarios")]
    [InlineData("https://EXEMPLO.test/a")]
    [InlineData("https://exemplo.test:443/a")]
    [InlineData("  https://exemplo.test/a  ")]
    public void CA_04_5_variacoes_do_mesmo_link_geram_um_hash_so(string variacao)
    {
        var esperado = NormalizadorItem.HashDoLink("https://exemplo.test/a");

        NormalizadorItem.HashDoLink(variacao).Should().Be(esperado);
    }

    [Fact]
    public void Parametro_que_identifica_o_conteudo_e_preservado()
    {
        var comId = NormalizadorItem.HashDoLink("https://exemplo.test/noticia?id=42");
        var semId = NormalizadorItem.HashDoLink("https://exemplo.test/noticia");

        comId.Should().NotBe(semId, "?id=42 aponta para outro conteúdo");
    }

    [Fact]
    public void Ordem_dos_parametros_nao_muda_o_hash()
    {
        var a = NormalizadorItem.HashDoLink("https://exemplo.test/x?b=2&a=1");
        var b = NormalizadorItem.HashDoLink("https://exemplo.test/x?a=1&b=2");

        a.Should().Be(b);
    }

    [Fact]
    public void Links_diferentes_geram_hashes_diferentes()
    {
        var a = NormalizadorItem.HashDoLink("https://exemplo.test/noticia-1");
        var b = NormalizadorItem.HashDoLink("https://exemplo.test/noticia-2");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Hash_tem_64_caracteres_hexadecimais_minusculos()
    {
        var hash = NormalizadorItem.HashDoLink("https://exemplo.test/a");

        hash.Should().HaveLength(64, "SHA-256 em hexadecimal");
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }
}
