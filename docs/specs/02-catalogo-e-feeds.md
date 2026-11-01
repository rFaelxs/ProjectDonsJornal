# 02 · Catálogo e validador de feeds

Cobre: RF07 (lado do catálogo), RF09, RF12, RF18, RN08.

## Fonte da verdade: `data/catalogo.json`

O catálogo é versionado no repositório e carregado no banco pelo comando `seed`, que faz upsert por `slug`.

```json
{
  "versao": 1,
  "categorias": [
    {
      "slug": "tecnologia-ia",
      "nome": "Tecnologia e IA",
      "descricao": "Notícias sobre inteligência artificial, desenvolvimento de software, cibersegurança, gadgets e grandes empresas de tecnologia.",
      "subtemas": [
        { "slug": "ia", "nome": "Inteligência artificial", "descricao": "Modelos de IA, pesquisa, regulação e produtos baseados em IA." },
        { "slug": "ciberseguranca", "nome": "Cibersegurança", "descricao": "Vazamentos de dados, ataques, golpes digitais e defesa." }
      ],
      "feeds": [
        { "veiculo": "Canaltech", "url": "<a confirmar>" },
        { "veiculo": "Meio Bit", "url": "<a confirmar>" },
        { "veiculo": "Tecnoblog", "url": "<a confirmar>" }
      ]
    }
  ]
}
```

- As 13 categorias e os subtemas de exemplo vêm da tabela do levantamento.
- A **descrição** de cada categoria e subtema tem 1 a 2 frases em linguagem natural. É ela que vira
  embedding (RF09), não o nome: "Cibersegurança" sozinho gera um vetor pobre.
- "Minha região" tem a região fixa em DF no MVP. A descrição menciona "Distrito Federal, Brasília".
- As URLs dos feeds serão levantadas e confirmadas no marco M1. Nenhuma URL entra como `ativo`
  sem passar pelo validador.

## Embeddings do catálogo (RF09)

O `seed` gera o embedding de `"{nome}: {descricao}"` para cada categoria e subtema. Só regera quando
o texto muda (comparando com um hash guardado) ou quando muda o modelo de embedding.

## Validador de feeds (RF18)

Comando `jornal validar-feeds [--categoria <slug>] [--atualizar]`.

Para cada URL:

| Passo | Regra | Falha → `motivo` |
|-------|-------|------------------|
| 1. Responde | `GET` com timeout de 20 s, segue até 3 redirects e envia um User-Agent identificável. Status 2xx. | `http_<status>` / `timeout` |
| 2. Formato | `SyndicationFeed.Load` lê o conteúdo como RSS 2.0 ou Atom 1.0. Se o XML for inválido, tenta sanitizar (BOM, entidades HTML comuns) uma vez. | `formato_invalido` |
| 3. Itens recentes | Pelo menos 3 itens com data nos últimos 7 dias. | `sem_itens_recentes` |
| 4. Campos | Pelo menos 80% dos itens têm título, link e data. | `campos_incompletos` |

Saída: tabela no terminal mais uma linha em `feed_validacao`. Com `--atualizar`, o comando marca
`feed.ativo` conforme o resultado.

Revalidação: o ciclo diário reaproveita os dados da coleta. Um feed com 3 falhas consecutivas fica
`ativo = false` e aparece num aviso na página de administração.

## RN08: pelo menos 2 feeds ativos por categoria

- Ao final do `validar-feeds`, o comando lista as categorias com menos de 2 feeds ativos.
- Uma categoria com menos de 2 feeds ativos fica **indisponível para escolha** no onboarding,
  e aparece desabilitada com o motivo.
- Se uma categoria já escolhida cair abaixo de 2 durante o uso, a edição continua com o feed que
  restar (RF17) e a página do perfil mostra um aviso.

## Critérios de aceite

- **CA-02.1** O `seed` é idempotente: rodar duas vezes não duplica dados nem regera embeddings sem mudança.
- **CA-02.2** O validador reprova uma URL que responde 404, um HTML que não é feed e um feed cujo item mais recente tem mais de 7 dias, cada um com o `motivo` correto. Testado com servidor HTTP fake.
- **CA-02.3** O validador aprova um RSS 2.0 e um Atom válidos, com itens recentes.
- **CA-02.4** A API do onboarding não aceita uma categoria com menos de 2 feeds ativos.
