namespace Jornal.Core.Coleta;

/// <summary>
/// Divide um item em trechos indexáveis (spec 04, RF14). Como só guardamos título e resumo
/// (RN02), o texto é curto e quase 100% dos itens viram um trecho só.
/// </summary>
public static class DivisorDeTrechos
{
    /// <summary>Abaixo disto o item vira um trecho único.</summary>
    public const int LimiteTrechoUnico = 1200;

    /// <summary>Tamanho alvo de cada janela, quando a divisão acontece.</summary>
    public const int TamanhoJanela = 800;

    /// <summary>
    /// Monta o texto base do item: <c>"{titulo}. {resumo}"</c>. É este texto que vai para
    /// o embedding.
    /// </summary>
    public static string TextoBase(string titulo, string resumo)
    {
        var t = titulo.Trim();
        var r = resumo.Trim();

        if (t.Length == 0) return r;
        if (r.Length == 0) return t;

        // Evita "Título.. resumo" quando o título já termina em pontuação.
        var separador = t.EndsWith('.') || t.EndsWith('!') || t.EndsWith('?') ? " " : ". ";
        return t + separador + r;
    }

    /// <summary>
    /// Divide em janelas de cerca de <see cref="TamanhoJanela"/> caracteres, cortando em fim
    /// de frase, com uma frase de sobreposição. Todo trecho repete o título no começo, para
    /// não perder o contexto (spec 04).
    /// </summary>
    public static IReadOnlyList<string> Dividir(string titulo, string resumo)
    {
        var texto = TextoBase(titulo, resumo);

        if (texto.Length == 0) return [];
        if (texto.Length <= LimiteTrechoUnico) return [texto];

        var frases = SepararFrases(texto);
        var trechos = new List<string>();
        var atual = new List<string>();
        var tamanho = 0;

        var prefixo = titulo.Trim().Length > 0 ? titulo.Trim() + ". " : string.Empty;

        foreach (var frase in frases)
        {
            // Fecha a janela quando ela já tem conteúdo e a próxima frase a estouraria.
            if (tamanho > 0 && tamanho + frase.Length > TamanhoJanela)
            {
                trechos.Add(Montar(prefixo, atual));

                // Uma frase de sobreposição, para o contexto não quebrar na fronteira.
                var ultima = atual[^1];
                atual = [ultima];
                tamanho = ultima.Length;
            }

            atual.Add(frase);
            tamanho += frase.Length;
        }

        if (atual.Count > 0) trechos.Add(Montar(prefixo, atual));

        return trechos;
    }

    private static string Montar(string prefixo, List<string> frases)
    {
        var corpo = string.Join(' ', frases).Trim();

        // Não repete o título quando a janela já começa com ele (o primeiro trecho).
        return corpo.StartsWith(prefixo.TrimEnd(), StringComparison.Ordinal)
            ? corpo
            : prefixo + corpo;
    }

    /// <summary>
    /// Separação por pontuação terminal seguida de espaço. Não é análise linguística — é o
    /// suficiente para texto de resumo de notícia, e não depende de dicionário.
    /// </summary>
    private static List<string> SepararFrases(string texto)
    {
        var frases = new List<string>();
        var inicio = 0;

        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] is not ('.' or '!' or '?')) continue;

            // Só corta se houver espaço depois, para não quebrar em "Dr." ou "R$ 1.500".
            var proximo = i + 1 < texto.Length ? texto[i + 1] : ' ';
            if (proximo is not (' ' or '\n' or '\r' or '\t')) continue;

            var frase = texto[inicio..(i + 1)].Trim();
            if (frase.Length > 0) frases.Add(frase);
            inicio = i + 1;
        }

        var resto = texto[inicio..].Trim();
        if (resto.Length > 0) frases.Add(resto);

        return frases;
    }
}
