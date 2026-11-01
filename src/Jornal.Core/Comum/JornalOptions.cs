namespace Jornal.Core.Comum;

/// <summary>
/// Seção <c>Jornal</c> do appsettings.json. Todos os valores numéricos de corte foram
/// propostos na spec 00 e serão calibrados com dados reais no marco M3, com o comando
/// <c>jornal calibrar</c>.
/// </summary>
public class JornalOptions
{
    public const string Secao = "Jornal";

    public RecuperacaoOptions Recuperacao { get; set; } = new();
    public ColetaOptions Coleta { get; set; } = new();
    public EdicaoOptions Edicao { get; set; } = new();
    public PerfilOptions Perfil { get; set; } = new();
    public AiOptions Ai { get; set; } = new();
    public EntregaOptions Entrega { get; set; } = new();
    public PromptsOptions Prompts { get; set; } = new();
}

public class RecuperacaoOptions
{
    /// <summary>Janela da pauta do dia (RF19).</summary>
    public int JanelaHoras { get; set; } = 24;

    /// <summary>Similaridade de cosseno mínima entre o tema e o trecho (RN03).</summary>
    public double LimiarSimilaridade { get; set; } = 0.55;

    /// <summary>Candidatos por tema antes do agrupamento.</summary>
    public int TopKPorTema { get; set; } = 12;

    /// <summary>Acima disto, dois trechos são o mesmo fato e viram um assunto (RF20).</summary>
    public double LimiarMesmoFato { get; set; } = 0.82;

    /// <summary>Até quantos dias atrás buscar contexto histórico (RF21).</summary>
    public int RelacionadosDias { get; set; } = 30;

    /// <summary>Máximo de relacionados por assunto (RF21).</summary>
    public int RelacionadosMax { get; set; } = 2;

    /// <summary>Limiar próprio dos relacionados, mais frouxo que o da base (spec 05, passo 4).</summary>
    public double LimiarRelacionados { get; set; } = 0.60;
}

public class ColetaOptions
{
    /// <summary>Acima disto o item é duplicata e não gera trechos (RF15).</summary>
    public double LimiarDuplicata { get; set; } = 0.93;

    /// <summary>Janela de comparação para duplicatas.</summary>
    public int DuplicataJanelaHoras { get; set; } = 72;

    /// <summary>Tempo limite por feed.</summary>
    public int TimeoutSegundos { get; set; } = 20;

    /// <summary>Feeds lidos ao mesmo tempo (spec 04).</summary>
    public int ParalelismoMaximo { get; set; } = 4;

    /// <summary>Na primeira coleta de um feed, ignora itens mais velhos que isto.</summary>
    public int IdadeMaximaDiasPrimeiraColeta { get; set; } = 7;
}

public class EdicaoOptions
{
    /// <summary>Nome impresso no cabeçalho. Identidade própria, não imita jornal real (RN06).</summary>
    public string Nome { get; set; } = "O Seu Jornal";

    /// <summary>Total de matérias, sem contar as sugestões.</summary>
    public int MaxMaterias { get; set; } = 8;

    /// <summary>Bloco "Talvez você goste" (RF30).</summary>
    public int MaxSugestoes { get; set; } = 2;

    /// <summary>Limite rígido de páginas A4 (RF32, RNF05).</summary>
    public int MaxPaginas { get; set; } = 2;

    /// <summary>QR codes monocromáticos de 14 mm por matéria (RNF03, RNF04).</summary>
    public bool QrCodes { get; set; }
}

public class PerfilOptions
{
    /// <summary>Exatamente 4 categorias por perfil (RN07).</summary>
    public int CategoriasExigidas { get; set; } = 4;

    /// <summary>Mínimo de feeds ativos para a categoria aparecer no onboarding (RN08).</summary>
    public int MinimoFeedsAtivosPorCategoria { get; set; } = 2;

    /// <summary>Corte das sugestões de tema auxiliar (spec 03, passo 5).</summary>
    public double LimiarSugestaoAuxiliar { get; set; } = 0.50;

    /// <summary>Quantas sugestões auxiliares mostrar no onboarding (RF10).</summary>
    public int MaxSugestoesAuxiliares { get; set; } = 5;
}

public class AiOptions
{
    public ChatOptions Chat { get; set; } = new();
    public EmbeddingOptions Embedding { get; set; } = new();
}

public class ChatOptions
{
    /// <summary><c>Ollama</c> ou <c>OpenAICompatible</c>. Trocável sem tocar no resto (spec 00).</summary>
    public string Provider { get; set; } = "Ollama";

    public string Model { get; set; } = "qwen2.5:7b-instruct";
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Nunca fica no appsettings — vem de variável de ambiente ou user-secrets.</summary>
    public string? ApiKey { get; set; }
}

public class EmbeddingOptions
{
    /// <summary>
    /// Sempre local. Trocar o modelo exige reindexar o acervo inteiro, por isso ele
    /// precisa ficar estável (spec 00).
    /// </summary>
    public string Model { get; set; } = "bge-m3";

    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>Textos por chamada ao gerador (spec 04).</summary>
    public int TamanhoLote { get; set; } = 32;
}

public class EntregaOptions
{
    /// <summary>Onde os PDFs são gravados (RF35).</summary>
    public string Pasta { get; set; } = "data/edicoes";
}

public class PromptsOptions
{
    public string Manchetes { get; set; } = "prompts/manchetes.v1.md";
    public string Verificador { get; set; } = "prompts/verificador.v1.md";
    public string ExtrairTemas { get; set; } = "prompts/extrair-temas.v1.md";
    public string InterpretarFeedback { get; set; } = "prompts/interpretar-feedback.v1.md";
}
