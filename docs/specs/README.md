# Jornal Personalizado: especificações

Specs de implementação derivadas do documento *Jornal Personalizado: levantamento de requisitos*.
O levantamento diz **o que** o sistema faz; estas specs dizem **como** vamos construir, com modelo
de dados, contratos, parâmetros e critérios de aceite.

## Índice

| # | Spec | Requisitos cobertos |
|---|------|---------------------|
| 00 | [Arquitetura e decisões](00-arquitetura.md) | Arquitetura sugerida, decisões em aberto |
| 01 | [Modelo de dados](01-modelo-de-dados.md) | Base para todas as specs |
| 02 | [Catálogo e validador de feeds](02-catalogo-e-feeds.md) | RF07, RF09, RF12, RF18, RN08 |
| 03 | [Perfil e interesses](03-perfil-e-interesses.md) | RF01–RF11, RN07 |
| 04 | [Coleta e indexação](04-coleta-e-indexacao.md) | RF13–RF17, RN02 |
| 05 | [Recuperação](05-recuperacao.md) | RF19–RF23, RN03, RN04 |
| 06 | [Geração da edição](06-geracao-da-edicao.md) | RF24–RF32, RN01, RN05, RN06 |
| 07 | [PDF, agendamento e entrega](07-pdf-agendamento-entrega.md) | RF33–RF35, RN09, RNF01–RNF05 |
| 08 | [Feedback e histórico](08-feedback-e-historico.md) | RF36–RF42, RN10, RNF06 |
| 09 | [MVP, marcos e demonstração](09-mvp-e-demo.md) | Escopo do MVP, plano B |

## Convenções

- **IDs**: `RFxx`, `RNxx` e `RNFxx` são os IDs do levantamento. Cada spec diz quais cobre.
- **Critérios de aceite** ficam no fim de cada spec, como `CA-<spec>.<n>`, e viram testes.
- **Parâmetros** configuráveis ficam em `appsettings.json`, na seção `Jornal`, e estão listados em
  [00-arquitetura.md](00-arquitetura.md#parâmetros-configuráveis).
- **⚠ Decisão proposta**: escolha feita aqui para destravar o trabalho, ainda pendente de confirmação.
- Código, nomes de tabelas e identificadores em português sem acento (`edicao`, `materia`),
  exceto termos técnicos consagrados (`embedding`, `chunk`, `feed`).

## Glossário

| Termo | Significado |
|-------|-------------|
| Categoria | Tema geral do catálogo (ex.: "Tecnologia e IA"). O usuário escolhe exatamente 4. |
| Subtema | Recorte dentro de uma categoria (ex.: "cibersegurança"). Opcional. |
| Tema específico | Texto livre do usuário (ex.: "concurso do Banco Central"). Vai para as manchetes. |
| Tema auxiliar | Categoria ou subtema do catálogo sugerido por proximidade vetorial (RF10). |
| Item | Uma notícia coletada de um feed (título, resumo, link, data, fonte). |
| Trecho | Pedaço de um item, com embedding. Na prática quase sempre é o item inteiro (ver spec 04). |
| Assunto | Um grupo de itens que tratam do mesmo fato em fontes diferentes (RF20). Vira uma matéria. |
| Matéria | Bloco da edição: título, linha fina, texto curto e fontes. |
| Edição | O jornal do dia, com 1 a 2 páginas A4, fechado com uma marca de fim. |
| Ciclo | Coleta → indexação → recuperação → geração → PDF, executado no horário do usuário. |
