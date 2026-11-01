# 05 · Recuperação

Cobre: RF19–RF23, RN03, RN04.

A saída desta etapa é a **pauta da edição**: a lista ordenada de assuntos com os trechos de base e os
relacionados. O LLM não escolhe o que entra no jornal, só redige. Toda decisão de pauta é
determinística e testável.

```csharp
record PautaDaEdicao(Guid PerfilId, int PerfilVersao, DateTimeOffset Referencia,
                     IReadOnlyList<Assunto> Assuntos, IReadOnlyList<Assunto> Sugestoes,
                     IReadOnlyList<TemaSemMateria> Omitidos);

record Assunto(string Secao, TipoMateria Tipo, Guid? TemaPerfilId, Guid? CategoriaId,
               double Relevancia, int Prioridade, Profundidade Profundidade,
               IReadOnlyList<TrechoRecuperado> Base,          // mesmo fato, várias fontes
               IReadOnlyList<TrechoRecuperado> Relacionados); // RF21, só para temas específicos
```

## Passo 1: recuperação por tema (RF19, RN03)

Para cada tema ativo do perfil (específicos, categorias com seus subtemas, auxiliares aceitos):

```sql
SELECT t.*, 1 - (t.embedding <=> @q) AS sim
FROM trecho t
WHERE t.publicado_em >= @agora - interval '24 hours'
  AND (@categorias IS NULL OR t.categoria_id = ANY(@categorias))
ORDER BY t.embedding <=> @q
LIMIT @topK;          -- 12
```

- **Vetor de consulta**: o embedding do tema. Quando a categoria tem subtemas escolhidos, faz uma
  consulta por subtema e junta os resultados.
- **Filtro de categoria**: temas específicos e auxiliares **não** filtram por categoria, porque o
  assunto pode estar em qualquer feed. Categorias filtram pela própria categoria.
- **Limiar (RN03)**: descarta trechos com `sim < 0.55`. Se não sobra nenhum, o tema vai para
  `Omitidos` com o motivo `abaixo_do_limiar` e **não ganha seção**. Nada é completado com conteúdo
  de fora do acervo.

## Passo 2: agrupamento do mesmo fato (RF20)

Os candidatos de **todos** os temas entram num agrupamento só, para que o mesmo fato não apareça em
duas seções:

1. Ordena os trechos por similaridade com o tema, do maior para o menor.
2. Agrupamento guloso: cada trecho entra no primeiro grupo cujo **centróide** tenha similaridade ≥ 0.82
   com ele. Se não houver, abre um grupo novo.
3. Cada grupo é um `Assunto` e pertence ao tema de maior `prioridade × sim` entre os seus trechos.
4. Dentro do assunto, fica no máximo 1 trecho por fonte (o de maior similaridade) e no máximo 4 fontes.

## Passo 3: seleção e ordenação (RF29, RF04)

```
relevancia(assunto) = max_sim × (1 + 0.15 × (nº_fontes − 1))   // fato em várias fontes pesa mais
score(assunto)      = prioridade_do_tema × relevancia
```

| Vaga | Regra |
|------|-------|
| **Principal** (1) | O assunto de maior score entre os **temas específicos**. Se nenhum tema específico tiver material, o maior score geral. |
| **Manchetes de tema específico** | No máximo 1 assunto por tema específico, ordenados por score. |
| **Seções** | Para cada uma das 4 categorias, os assuntos de maior score, de 1 a 2 por categoria. A ordem das seções segue a prioridade da categoria. |
| **Sugestões (RF30)** | Até 2 assuntos vindos dos temas auxiliares aceitos, que não estejam já na pauta. |
| **Limite** | `MaxMaterias = 8`, sem contar as sugestões. Corta pelo menor score, mas mantém pelo menos 1 assunto por seção quando houver material. |

**Manchete dupla (RF27)**: se o 2º assunto de maior score também for de tema específico e tiver score
≥ 85% do primeiro, a pauta marca os dois para a principal sair no formato "A + B". Os dois assuntos
são redigidos juntos e o segundo não aparece de novo.

## Passo 4: conteúdos relacionados (RF21, RF22, RN04)

Só para assuntos de **tema específico**:

- Consulta com o centróide do assunto, `publicado_em` entre `agora − 30 dias` e `agora − 24h`.
  O limite de 30 dias vem de `RelacionadosDias`.
- Sem filtro de categoria, com limiar de similaridade de 0.60.
- **RN04**: exclui os itens do próprio assunto, os itens com `duplicata_de` apontando para eles e
  qualquer trecho com similaridade ≥ 0.82 com o centróide, porque seria a mesma notícia.
- Prefere a diversidade temporal: no máximo 1 por dia, e no máximo 2 no total.
- Cada relacionado leva a data e a fonte (RF22).
- **Status (RF22)**: não é calculado na recuperação. O redator pode emitir o campo `status` (por exemplo,
  "inscrições abertas") **somente se** o texto do trecho afirma isso. O verificador confere (spec 06).

## Passo 5: rastreabilidade (RF23)

Ao persistir a edição, cada `TrechoRecuperado` usado vira um `materia_fonte` (`papel = base` ou
`relacionado`), com o `item_id`, de onde saem a fonte e o link. Temas omitidos ficam registrados no
log do ciclo.

## Critérios de aceite

Os testes usam um gerador de embeddings fake com vetores controlados.

- **CA-05.1** Um tema cujo melhor trecho tem similaridade 0.50 fica em `Omitidos` e não gera seção.
- **CA-05.2** Três trechos do mesmo fato em fontes diferentes viram 1 assunto com 3 fontes.
- **CA-05.3** Um trecho de 25 horas atrás não entra como base. Um de 10 dias pode entrar como relacionado, e um de 40 dias não.
- **CA-05.4** Um relacionado nunca é um item do próprio assunto nem uma duplicata dele.
- **CA-05.5** Com 2 temas específicos com material, a principal é de tema específico mesmo que uma categoria tenha maior similaridade bruta.
- **CA-05.6** A pauta nunca passa de 8 matérias + 2 sugestões.
