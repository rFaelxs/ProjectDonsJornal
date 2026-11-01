# 09 · MVP, marcos e demonstração

## Escopo do MVP

- 1 usuário real (o apresentador) e, opcionalmente, alguns participantes do teste de usuário.
- As 13 categorias ficam no catálogo, mas **só as categorias da demo precisam de feeds validados**.
  Proposta: Tecnologia e IA, Carreira e emprego, Economia e finanças e Esportes. Esportes entra para
  o roteiro "menos futebol" funcionar.
- 2 ou 3 temas específicos no perfil da demo (ex.: "cibersegurança", "concurso do Banco Central",
  "novidades do .NET").
- Acervo pré-carregado com **pelo menos 7 dias** de coleta antes da apresentação, para os relacionados
  (RF21) terem histórico.

### Fora do escopo

Mantém a lista do levantamento: temas a evitar e opção "Outros", módulo de concursos, busca web livre,
leitura do texto completo das páginas e mais de um estilo. Acrescenta: multiusuário com login, envio
por e-mail e impressão automática.

## Marcos

Cada marco termina com algo demonstrável e com os critérios de aceite da spec correspondente passando.

| Marco | Entrega | Specs |
|-------|---------|-------|
| **M0 · Fundação** | Solução criada, `docker-compose` (Postgres+pgvector, Ollama com `bge-m3` e modelo de chat), EF Core com migrations, testes rodando no CI local | 00, 01 |
| **M1 · Catálogo e feeds** | `catalogo.json` completo, URLs levantadas, `validar-feeds` rodando, ≥ 2 feeds ativos nas 4 categorias da demo | 02 |
| **M2 · Acervo** | `coletar` + indexação + dedup. **Começar a coleta diária já no M2** (agendador simples ou Agendador de Tarefas do Windows chamando o CLI) para acumular histórico | 04 |
| **M3 · Perfil e recuperação** | Onboarding web completo; `jornal pauta` imprime a pauta no terminal; calibração de limiares com dados reais | 03, 05 |
| **M4 · Geração** | Prompts v1 com few-shot, redator + verificador, edição em JSON/HTML | 06 |
| **M5 · PDF e ciclo** | PDF A4, ciclo agendado, RN09, página `/edicoes`; teste de impressão | 07 |
| **M6 · Feedback** | Avaliação, interpretação, confirmação, edição 2 ajustada, histórico e exportação | 08 |
| **M7 · Demo** | Roteiro ensaiado, comparação com e sem RAG, plano B gravado | 09 |

O caminho crítico é **M2 cedo**: sem dias de acervo acumulado não existe demonstração de relacionados.

### Calibração no M3

Comando `jornal calibrar --tema "<texto>"`: mostra os 20 trechos mais próximos das últimas 24h com
a similaridade, para escolher `LimiarSimilaridade`, `LimiarMesmoFato` e `LimiarDuplicata` olhando
dados reais. Os valores escolhidos e a justificativa ficam anotados em `docs/calibracao.md`.

## Roteiro da demonstração (10 min)

| Tempo | Momento | O que mostrar |
|-------|---------|---------------|
| 0:00–1:30 | Problema | Rolagem infinita × jornal com começo e fim |
| 1:30–3:00 | Arquitetura RAG | Diagrama da spec 00: RSS → embeddings → pgvector → LLM restrito aos trechos |
| 3:00–4:00 | Perfil | Tela do perfil já preenchido: 4 categorias, temas específicos e auxiliares aceitos |
| 4:00–5:30 | **Edição 1** | `jornal gerar` ao vivo (ou PDF pronto), PDF aberto, uma matéria com fonte e um relacionado |
| 5:30–7:00 | **Com × sem RAG** | `jornal comparar-rag --tema "cibersegurança"`: lado a lado, o modelo sozinho (inventa ou diz que não sabe) e com RAG (fato de ontem com fonte) |
| 7:00–9:00 | **Feedback → edição 2** | Comentário "menos futebol, mais cibersegurança", ajustes propostos, confirmação, `jornal gerar --data <amanhã simulada>` e diferença visível |
| 9:00–10:00 | Fechamento | Limitações (RN01 em ação: matéria descartada pelo verificador) e próximos passos |

`jornal gerar --data` permite gerar a "edição seguinte" na hora, usando o acervo já coletado e o
perfil na nova versão, sem esperar o dia virar.

### `comparar-rag`

Faz a mesma pergunta ("Quais as notícias de hoje sobre {tema}?") ao mesmo `IChatClient`:
(a) sem contexto e (b) com os trechos recuperados e o prompt de manchetes. Imprime as duas respostas
lado a lado, e a versão (b) lista as fontes.

## Plano B

- `data/demo/edicao-1.pdf` e `data/demo/edicao-2.pdf` gerados na véspera.
- Dump do banco da véspera (`data/demo/acervo.dump`) para restaurar se o Postgres der problema.
- Vídeo de 2 a 3 minutos do roteiro "Feedback → edição 2", gravado no M7.
- Se o Ollama estiver lento na máquina da apresentação: trocar `Ai:Chat:Provider` para a API externa
  (já configurada e testada no M7).

## Riscos

| Risco | Mitigação |
|-------|-----------|
| Feeds brasileiros sem RSS ou com RSS quebrado | Validador no M1; ≥ 3 candidatos por categoria da demo; RN08 |
| Modelo 7B local fraco em humor e JSON | Saída estruturada + nova tentativa; provider de chat trocável |
| Pouco acervo para relacionados | Coleta contínua a partir do M2 |
| Verificador reprovando demais e edição vazia | Calibrar o prompt no M4; registrar a taxa de descarte; mínimo de 1 matéria + marca de fim |
| Geração lenta ao vivo | Gerar a edição 1 antes; ao vivo só a edição 2, com `--sem-coleta` |
