namespace Jornal.Core.Comum;

/// <summary>Quanto texto uma matéria recebe. Ajustável pelo feedback (spec 08).</summary>
public enum Profundidade
{
    /// <summary>Só título e linha fina.</summary>
    Breve,
    /// <summary>2 a 3 linhas.</summary>
    Normal,
    /// <summary>4 linhas e relacionados com uma frase cada.</summary>
    Detalhada
}

/// <summary>Papel de uma matéria na edição (spec 05, passo 3).</summary>
public enum TipoMateria
{
    /// <summary>A matéria em destaque, no topo. Uma por edição.</summary>
    Principal,
    Normal,
    /// <summary>Bloco "Talvez você goste" (RF30).</summary>
    Sugestao
}

/// <summary>Como um item sustenta uma matéria (RF23).</summary>
public enum PapelFonte
{
    /// <summary>Trecho da janela de 24h que originou a matéria.</summary>
    Base,
    /// <summary>Contexto histórico de até 30 dias (RF21).</summary>
    Relacionado
}
