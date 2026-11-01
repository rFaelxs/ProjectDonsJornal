# 06 · Geração da edição

Cobre: RF24–RF32, RN01, RN02, RN05, RN06.

## Visão

```
PautaDaEdicao ──▶ Redator (1 chamada por assunto) ──▶ Verificador (1 chamada por matéria)
                                                          │ aprovada → Edicao.materias
                                                          │ reprovada → 1 nova tentativa com o motivo
                                                          └ reprovada de novo → descartada (RN01)
```

Uma chamada por assunto (e não uma para a edição toda) mantém o contexto pequeno, o que funciona bem
com modelo local, permite refazer só a matéria que falhou e deixa a rastreabilidade óbvia.

## Template de estilo (RF28, RN06)

Arquivo `prompts/manchetes.v1.md`, carregado em tempo de execução. O caminho fica em
`Jornal:Prompts:Manchetes`, então trocar de estilo é trocar de arquivo, sem recompilar.

Estrutura do arquivo:

```markdown
---
id: manchetes
versao: 1
---
## Sistema
Você é o redator de um jornal impresso pessoal. Escreve em português do Brasil.
Regras:
1. Use SOMENTE as informações dos TRECHOS. Não acrescente números, nomes, datas ou causas que não estejam neles.
2. Título: até 70 caracteres, direto. Pode ter trocadilho ou humor leve, desde que o fato continue verdadeiro.
3. Linha fina: uma frase, até 140 caracteres, que explica o título.
4. Texto: 2 a 4 linhas (entre 220 e 420 caracteres), resumindo. Não copie frases inteiras dos trechos.
5. Se os trechos não sustentam uma matéria, responda {"sem_base": true}.
6. Responda apenas o JSON no formato pedido.

## Exemplos
<!-- 4 a 6 exemplos ORIGINAIS do projeto (RN06): trechos fictícios → JSON esperado.
     Incluir: 1 título sério, 2 com trocadilho, 1 manchete dupla "A + B", 1 caso sem_base. -->

## Usuário
Seção: {{secao}} | Profundidade: {{profundidade}} | Formato: {{formato}}  <!-- simples | duplo -->
TRECHOS:
{{#trechos}}[{{id}}] ({{fonte}}, {{data}}) {{texto}}
{{/trechos}}
RELACIONADOS:
{{#relacionados}}[{{id}}] ({{fonte}}, {{data}}) {{texto}}
{{/relacionados}}
```

Os exemplos few-shot são escritos por nós, com fatos e veículos fictícios. Nunca reproduzem manchetes,
marcas ou layout de publicações reais (RN06).

**Profundidade** (vem do feedback, spec 08): `breve` = só título e linha fina; `normal` = 2 a 3 linhas;
`detalhada` = 4 linhas e relacionados com uma frase cada.

## Saída do redator (JSON estruturado)

```json
{
  "titulo": "string",
  "linha_fina": "string",
  "texto": "string",
  "fontes_usadas": ["t_812", "t_815"],
  "relacionados": [ { "id": "t_301", "frase": "string", "status": "inscrições abertas" | null } ],
  "sem_base": false
}
```

- Usa `IChatClient.GetResponseAsync<T>` (saída estruturada do Microsoft.Extensions.AI), com
  temperatura 0.4 e 1 nova tentativa se o JSON vier inválido.
- **Validação local** (sem LLM), antes do verificador:
  - `fontes_usadas` não pode ser vazio e precisa ser subconjunto dos IDs fornecidos.
  - Limites de tamanho: título ≤ 70, linha fina ≤ 140, texto entre 150 e 450 caracteres.
  - Formato duplo (RF27): o título contém exatamente um " + ".
  - Nenhuma sequência de mais de 12 palavras copiada literalmente de um trecho (RN02).
- `sem_base = true`: a matéria é descartada com motivo `sem_base` (RN01).

## Verificador (RN01, RN05)

Segunda chamada ao LLM, com `prompts/verificador.v1.md` e temperatura 0:

> Dados os TRECHOS e a MATÉRIA, liste cada afirmação factual do título, da linha fina, do texto e dos
> status. Para cada uma, diga `sustentada` ou `nao_sustentada`, com o ID do trecho que a sustenta.
> Trocadilho ou figura de linguagem é aceitável se o fato subjacente estiver sustentado.

```json
{ "afirmacoes": [ { "texto": "...", "veredito": "sustentada", "trecho": "t_812" } ], "aprovada": true }
```

- `aprovada = false` se houver qualquer `nao_sustentada`.
- Reprovada: o sistema chama o redator de novo, uma vez, incluindo as afirmações não sustentadas como
  "não diga isto". Se reprovar de novo, a matéria é descartada (`motivo_descarte = "verificador"`).
- Se a principal for descartada, o próximo assunto de maior score assume o lugar.

## Citação de fonte (RF31)

Não é o LLM que escreve a fonte. O renderizador monta o rodapé de cada matéria a partir de `materia_fonte`:

> *Fontes: Canaltech, 31/10 · Tecnoblog, 31/10*

Relacionados aparecem como: *Antes: "frase do redator" (Agência Brasil, 18/10) · inscrições abertas*.

## Montagem da edição (RF29, RF30, RF32)

Ordem fixa no documento:

1. **Cabeçalho**: nome do jornal (configurável, padrão "O Seu Jornal"), número da edição, data por
   extenso e a frase "Edição fechada às HH:mm".
2. **Principal**, em destaque.
3. **Manchetes dos temas específicos.**
4. **Seções por categoria**, na ordem de prioridade.
5. **"Talvez você goste"**: no máximo 2 sugestões (RF30), em formato `breve`.
6. **Marca de fim (RF32)**: `— Fim da edição nº N —`, seguida da linha "Você chegou ao fim. Bom dia."
   e de um bloco curto "Avalie esta edição" com o endereço da página de feedback (impresso, e
   QR code opcional).

Se a renderização passar de 2 páginas (RNF05), o sistema remove a matéria de menor score que não seja
a principal nem a única da seção e renderiza de novo, até caber.

## Critérios de aceite

- **CA-06.1** Uma resposta do redator citando um ID de trecho que não foi fornecido é rejeitada pela validação local.
- **CA-06.2** Com o verificador fake reprovando duas vezes, a matéria fica gravada com `verificada = false` e não aparece no PDF.
- **CA-06.3** Trocar `Prompts:Manchetes` para outro arquivo muda o prompt sem recompilar.
- **CA-06.4** Toda matéria publicada tem pelo menos 1 `materia_fonte` com `papel = base`.
- **CA-06.5** A edição sempre termina com a marca de fim, mesmo quando só uma matéria sobrevive.
- **CA-06.6** Avaliação manual, numa amostra de 20 matérias reais: nenhuma com fato inventado. É um checklist do marco M4, não um teste automatizado.
