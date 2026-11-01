# 04 · Coleta e indexação

Cobre: RF13–RF17, RN02.

## Coleta (RF13, RF17)

`ColetaService.ColetarAsync` lê **todos os feeds ativos das categorias usadas por algum perfil**. No MVP,
isso é tudo o que estiver ativo.

- Leitura em paralelo, com no máximo 4 feeds ao mesmo tempo, `HttpClient` com timeout de 20 s e
  política de retry de 1 tentativa extra (Polly via `Microsoft.Extensions.Http.Resilience`).
- Usa `If-Modified-Since` / `ETag` quando o feed oferece (os valores ficam guardados em `feed`).
- Um feed que falha gera uma linha em `coleta_falha`, incrementa `falhas_consecutivas` e a coleta
  **continua** com os outros. O ciclo só aborta se **todos** os feeds falharem.
- Um feed bem-sucedido zera `falhas_consecutivas` e atualiza `ultima_coleta_em`.

### Normalização de um item

| Campo | Origem | Tratamento |
|-------|--------|------------|
| `titulo` | `item.Title` | Remove HTML, decodifica entidades e faz trim |
| `resumo` | `item.Summary`, ou `content:encoded` se o summary vier vazio | Remove HTML e trunca em **600 caracteres** no fim de frase (RN02: nada de matéria inteira) |
| `link` | `item.Links[0]` ou `item.Id` | Canonicaliza: minúsculas no host, sem `utm_*`, `fbclid` e `#fragment` |
| `publicado_em` | `PublishDate`, ou `LastUpdatedTime`, ou o horário da coleta | Item sem data fica com a hora da coleta e um flag `data_estimada` |
| `fonte` | `feed.nome_veiculo` | |
| `categoria_id` | `feed.categoria_id` | Um veículo com feeds em várias categorias gera itens em cada uma, e a deduplicação resolve |

Itens com mais de 7 dias na primeira coleta de um feed são ignorados, para não poluir a janela de 24h.
O seed de acervo da demo usa outra rota (spec 09).

## Deduplicação (RF15)

Em duas etapas, aplicadas nesta ordem:

1. **Mesmo link**: se `link_hash` já existe, o item é ignorado e nem é gravado.
2. **Conteúdo muito similar**: depois de gerar o embedding, o sistema compara com os trechos das
   últimas 72 h. Se a similaridade de cosseno for ≥ 0.93, o item é gravado com `duplicata_de = <id>`
   e **não gera trechos**.

Notícias parecidas mas não idênticas (entre 0.82 e 0.93) **não** são duplicatas. Elas viram o mesmo
*assunto* na recuperação (RF20, spec 05), o que permite citar várias fontes.

## Indexação (RF14)

### Divisão em trechos

Como só usamos título e resumo (RN02), o item é curto. A regra é:

- Texto base: `"{titulo}. {resumo}"`.
- Se o texto base tem até 1.200 caracteres, o item vira **1 trecho** (o caso de quase 100% dos itens).
- Acima disso, divide em janelas de cerca de 800 caracteres, cortando em fim de frase, com 1 frase de
  sobreposição. Todo trecho repete o título no começo, para não perder o contexto.

### Embedding e gravação

- Lotes de 32 textos para `IEmbeddingGenerator.GenerateAsync`.
- Cada `trecho` guarda os metadados desnormalizados (`fonte`, `publicado_em`, `categoria_id`, RF14).
- A indexação é idempotente: processa itens sem trechos e sem `duplicata_de`.

## Acervo cumulativo (RF16)

- Nada é apagado. O acervo cresce em torno de 13 categorias × 2–3 feeds × ~30 itens por dia,
  ou seja, uns 1.000 itens/dia no máximo. É um volume tranquilo para o pgvector com HNSW.
- Backup: `pg_dump` manual, documentado no README do projeto. O volume Docker é persistente.

## Critérios de aceite

- **CA-04.1** Coletar duas vezes o mesmo feed fake não cria itens novos.
- **CA-04.2** Dois itens com links diferentes e embeddings com similaridade ≥ 0.93 (forçado pelo gerador fake): o segundo fica com `duplicata_de` e 0 trechos.
- **CA-04.3** Com 3 feeds e 1 deles devolvendo 500, a coleta grava os itens dos outros 2 e 1 `coleta_falha`.
- **CA-04.4** Nenhum `item.resumo` passa de 600 caracteres nem contém tags HTML.
- **CA-04.5** Link com `?utm_source=x#topo` e o mesmo link sem esses parâmetros geram o mesmo `link_hash`.
