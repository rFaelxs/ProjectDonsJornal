using FluentAssertions;
using Jornal.Core.Catalogo;
using Jornal.Core.Comum;
using Jornal.Core.Feedback;
using Jornal.Core.Geracao;
using Jornal.Core.Perfis;

namespace Jornal.Core.Tests;

/// <summary>
/// Os enums do domínio precisam cobrir exatamente os valores escritos na spec 01. Se alguém
/// renomear um membro, o banco passa a gravar outra string e o histórico fica inconsistente —
/// este teste trava isso.
/// </summary>
public class ContratoDoDominioTests
{
    [Theory]
    // spec 01, feed_validacao.formato
    [InlineData(typeof(FormatoFeed), "Rss,Atom,Invalido")]
    // spec 01, tema_perfil.tipo
    [InlineData(typeof(TipoTema), "Especifico,Auxiliar")]
    // spec 01, tema_perfil.profundidade
    [InlineData(typeof(Profundidade), "Breve,Normal,Detalhada")]
    // spec 01, edicao.status
    [InlineData(typeof(StatusEdicao), "Gerando,Pronta,Falhou")]
    // spec 01, materia.tipo
    [InlineData(typeof(TipoMateria), "Principal,Normal,Sugestao")]
    // spec 01, materia_fonte.papel
    [InlineData(typeof(PapelFonte), "Base,Relacionado")]
    // spec 01, sugestao_auxiliar.resposta
    [InlineData(typeof(RespostaSugestao), "Pendente,Aceito,Recusado")]
    // spec 08, conjunto fechado de operações de ajuste
    [InlineData(typeof(TipoAjuste),
        "AdicionarTema,RemoverTema,MudarPrioridade,MudarProfundidade,TrocarCategoria")]
    // spec 01, ajuste_perfil.status
    [InlineData(typeof(StatusAjuste), "Proposto,Confirmado,Rejeitado")]
    public void Enum_tem_exatamente_os_membros_da_spec(Type tipo, string esperados)
    {
        Enum.GetNames(tipo).Should().BeEquivalentTo(esperados.Split(','));
    }

    [Fact]
    public void RegraNegocioException_carrega_o_id_da_regra()
    {
        // CA-03.1: a UI e os testes afirmam sobre a regra, não sobre a mensagem.
        var ex = new RegraNegocioException("RN07");

        ex.Regra.Should().Be("RN07");
        ex.Message.Should().Contain("RN07");
    }

    [Fact]
    public void Score_do_assunto_e_prioridade_vezes_relevancia()
    {
        // spec 05, passo 3: score(assunto) = prioridade_do_tema × relevancia
        var assunto = new Recuperacao.Assunto(
            Secao: "Tecnologia e IA",
            Tipo: TipoMateria.Normal,
            TemaPerfilId: Guid.NewGuid(),
            CategoriaId: null,
            Relevancia: 0.8,
            Prioridade: 5,
            Profundidade: Profundidade.Normal,
            Base: [],
            Relacionados: []);

        assunto.Score.Should().BeApproximately(4.0, 1e-9);
    }
}
