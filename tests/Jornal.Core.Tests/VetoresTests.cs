using FluentAssertions;
using Jornal.Core.Comum;

namespace Jornal.Core.Tests;

/// <summary>
/// A similaridade de cosseno decide duplicata (RF15), agrupamento de assunto (RF20) e corte
/// de relevância (RN03). Se ela estiver errada, todo diagnóstico acima dela mente — por isso
/// é testada contra valores calculáveis à mão.
/// </summary>
public class VetoresTests
{
    [Fact]
    public void Vetores_identicos_dao_similaridade_1()
    {
        float[] v = [1, 2, 3];

        Vetores.SimilaridadeCosseno(v, [1, 2, 3]).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Vetores_ortogonais_dao_similaridade_0()
    {
        Vetores.SimilaridadeCosseno([1, 0], [0, 1]).Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void Vetores_opostos_dao_similaridade_menos_1()
    {
        Vetores.SimilaridadeCosseno([1, 2], [-1, -2]).Should().BeApproximately(-1.0, 1e-9);
    }

    [Fact]
    public void Escala_nao_muda_a_similaridade()
    {
        // Cosseno mede direção, não comprimento.
        Vetores.SimilaridadeCosseno([1, 2, 3], [10, 20, 30]).Should().BeApproximately(1.0, 1e-6);
    }

    [Fact]
    public void Valor_conhecido_calculado_a_mao()
    {
        // a=[1,0], b=[1,1] → cos = 1/(1*√2) = 0,7071
        Vetores.SimilaridadeCosseno([1, 0], [1, 1])
            .Should().BeApproximately(0.70710678, 1e-7);
    }

    [Fact]
    public void Vetor_nulo_nao_divide_por_zero()
    {
        Vetores.SimilaridadeCosseno([0, 0], [1, 2]).Should().Be(0);
    }

    [Fact]
    public void Dimensoes_diferentes_sao_erro_de_programacao()
    {
        var acao = () => Vetores.SimilaridadeCosseno([1, 2], [1, 2, 3]);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Similaridade_distingue_vetores_proximos_mas_nao_iguais()
    {
        // O caso que importa na prática: 0.93 separa duplicata de "mesmo assunto".
        float[] a = [1, 0, 0];
        float[] b = [0.95f, 0.05f, 0];

        var similaridade = Vetores.SimilaridadeCosseno(a, b);

        similaridade.Should().BeLessThan(1.0);
        similaridade.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public void Centroide_e_a_media_componente_por_componente()
    {
        var centroide = Vetores.Centroide([[0, 0], [2, 4], [4, 2]]);

        centroide.Should().Equal(2f, 2f);
    }

    [Fact]
    public void Centroide_de_um_vetor_so_e_ele_mesmo()
    {
        Vetores.Centroide([[1, 2, 3]]).Should().Equal(1f, 2f, 3f);
    }

    [Fact]
    public void Centroide_de_conjunto_vazio_e_erro()
    {
        var acao = () => Vetores.Centroide([]);

        acao.Should().Throw<ArgumentException>();
    }
}
