# 08 · Feedback e histórico

Cobre: RF36–RF42, RN10, RNF06.

## Tela de avaliação (`/edicoes/{numero}/avaliar`)

| Campo | Tipo | Requisito |
|-------|------|-----------|
| Nota | 1 a 5 estrelas, obrigatória | RF36 |
| Seções | Para cada seção da edição: 👍 / 😐 / 👎, opcional | RF42 |
| Comentário | Texto livre: "o que melhorar, o que sobrou, o que faltou" | RF37 |
| Novo tópico | Texto curto: "quero ver amanhã…" | RF38 |

Só existe uma avaliação por edição. Ela pode ser editada até a próxima edição ser gerada.

## Interpretação do comentário (RF39)

Ao enviar, o LLM recebe, com o prompt `prompts/interpretar-feedback.v1.md`:

- o perfil atual (categorias, temas, prioridades e profundidades);
- a lista de seções da edição com a avaliação de cada uma;
- o comentário e o novo tópico;
- o catálogo de categorias, só nomes e slugs.

E responde uma lista de ajustes, todos de um conjunto **fechado** de operações:

| `tipo` | `payload` | Exemplo de origem |
|--------|-----------|-------------------|
| `adicionar_tema` | `{ texto, tipo: "especifico", categoria_slug? }` | "mais cibersegurança", novo tópico |
| `remover_tema` | `{ tema_id }` | "pode tirar o .NET" |
| `mudar_prioridade` | `{ alvo: "tema"/"categoria", id, de, para }` (passo de ±1 ou ±2, entre 1 e 5) | "menos futebol" |
| `mudar_profundidade` | `{ alvo, id, de, para }` | "economia está muito raso" |
| `trocar_categoria` | `{ sai_slug, entra_slug }`, mantendo 4 (RN07) | "troca esportes por ciência" |

- Cada ajuste vem com uma `justificativa` curta, que cita o trecho do comentário que o motivou.
- O **novo tópico** (RF38) sempre gera um `adicionar_tema` específico, mesmo que o LLM não proponha.
- Validação local: IDs existentes, prioridade dentro do intervalo de 1 a 5, `trocar_categoria` com
  `entra_slug` disponível (RN08) e a contagem de categorias sempre 4. Ajustes inválidos são descartados
  e registrados no log.
- A nota sozinha não gera ajuste automático. Notas 👎 por seção entram no prompt como sinal.

## Confirmação (RF40, RN10)

Depois de enviar, a tela mostra a lista de mudanças propostas, em linguagem natural:

> ☑ Diminuir a prioridade de **Esportes** (3 → 1)
> *"menos futebol"*
> ☑ Adicionar o tema específico **cibersegurança** em Tecnologia e IA
> *"mais cibersegurança"*

- Cada item tem um checkbox (padrão marcado) e os botões **Confirmar selecionados** e **Descartar todos**.
- Confirmar: os ajustes ficam `confirmado`, e os não marcados ficam `rejeitado`. O perfil ganha uma nova
  `versao`, os temas novos ganham embeddings e todos recebem `valido_a_partir_da_edicao = proxima`.
- Nada muda no perfil sem confirmação, e nada confirmado afeta a edição que já foi gerada (RN10).
- Se a edição de amanhã já estiver sendo gerada, o ajuste vale para a seguinte.

## Histórico (RF41, RF42)

Tudo já fica nas tabelas da spec 01. O que é exigido especificamente:

- `edicao.perfil_versao` permite reconstruir qual perfil gerou cada edição.
- `/historico` mostra uma linha do tempo: edições (nota), ajustes confirmados e rejeitados e mudanças
  de categoria.
- **Exportação para o teste de usuário**: `jornal exportar-teste --saida dados.csv` gera um CSV por
  tabela-chave (edições, seções avaliadas, auxiliares aceitos ou recusados, ajustes), sempre
  identificando o usuário pelo `apelido`.

## Anonimização e consentimento (RNF06)

- O cadastro pede só um **apelido**. Não há nome, e-mail nem dado pessoal no MVP.
- O primeiro passo do onboarding é um termo de consentimento curto (o que é guardado, para quê e como
  pedir exclusão), com aceite obrigatório. `usuario.consentimento_em` registra o aceite.
- Na exportação, o comentário livre passa por uma remoção simples de PII (e-mails, telefones e CPFs por
  regex) antes de sair.
- Comando `jornal excluir-usuario <apelido>` apaga o perfil, as avaliações e as edições do usuário.
  O acervo de notícias, que não é dado pessoal, fica.

## Critérios de aceite

- **CA-08.1** Comentário "menos futebol, mais cibersegurança" com o LLM fake: gera 1 `mudar_prioridade` (Esportes ↓) e 1 `adicionar_tema` (cibersegurança). É o roteiro da demo.
- **CA-08.2** Ajustes não confirmados não alteram o perfil, e `perfil.versao` não muda.
- **CA-08.3** Um ajuste confirmado depois do início da geração da edição N+1 só aparece na edição N+2.
- **CA-08.4** Uma proposta de `trocar_categoria` que deixaria 3 ou 5 categorias é descartada.
- **CA-08.5** O CSV exportado não contém e-mail nem telefone presentes num comentário de teste.
