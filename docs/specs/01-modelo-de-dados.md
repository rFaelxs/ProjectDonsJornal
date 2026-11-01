# 01 · Modelo de dados

PostgreSQL com a extensão `vector`. Vetores `vector(1024)` (bge-m3), índice HNSW com
`vector_cosine_ops`. Datas em `timestamptz` (UTC); o fuso de exibição é `America/Sao_Paulo`.

## Diagrama

```
categoria 1─* subtema
categoria 1─* feed 1─* feed_validacao
                 feed 1─* coleta_falha
                 feed 1─* item 1─* trecho(embedding)

usuario 1─1 perfil 1─* perfil_categoria *─1 categoria
               perfil 1─* tema_perfil(embedding)          -- específicos e auxiliares aceitos
               perfil 1─* sugestao_auxiliar               -- RF11: aceitos/recusados

usuario 1─* edicao 1─* materia 1─* materia_fonte *─1 item
               edicao 1─0..1 avaliacao 1─* ajuste_perfil
ciclo_execucao (log de cada execução do ciclo)
```

## Tabelas

### Catálogo

| Tabela | Colunas principais |
|--------|--------------------|
| `categoria` | `id`, `slug` (único), `nome`, `descricao` (RF09), `embedding vector(1024)`, `ativa` |
| `subtema` | `id`, `categoria_id`, `slug`, `nome`, `descricao`, `embedding vector(1024)` |
| `feed` | `id`, `categoria_id`, `nome_veiculo`, `url` (único), `ativo`, `validado_em`, `ultima_coleta_em`, `falhas_consecutivas` |
| `feed_validacao` | `id`, `feed_id`, `executado_em`, `http_status`, `formato` (`rss`/`atom`/`invalido`), `itens_recentes`, `aprovado`, `motivo` |
| `coleta_falha` | `id`, `feed_id`, `ciclo_id`, `ocorreu_em`, `tipo` (`timeout`/`http`/`parse`/`outro`), `mensagem` (RF17) |

### Acervo

| Tabela | Colunas principais |
|--------|--------------------|
| `item` | `id`, `feed_id`, `categoria_id`, `fonte` (nome do veículo), `titulo`, `resumo` (texto puro, sem HTML), `link`, `link_hash` (único, SHA-256 do link canônico), `publicado_em`, `coletado_em`, `duplicata_de` (nullable, FK `item`) |
| `trecho` | `id`, `item_id`, `ordem`, `texto`, `embedding vector(1024)`, `publicado_em`, `categoria_id`, `fonte` (desnormalizados para filtrar sem join, RF14) |

Índices: `trecho USING hnsw (embedding vector_cosine_ops)`, `trecho (publicado_em)`,
`trecho (categoria_id, publicado_em)`, `item (link_hash) UNIQUE`.

O acervo **nunca é apagado** (RF16). Itens marcados como duplicata ficam guardados, mas não geram trechos.

### Perfil

| Tabela | Colunas principais |
|--------|--------------------|
| `usuario` | `id` (GUID), `apelido` (pseudônimo, RNF06), `consentimento_em`, `criado_em` |
| `perfil` | `id`, `usuario_id`, `texto_livre_original` (RF01), `horario_geracao` (`time`, RF05), `fuso`, `versao` (incrementa a cada ajuste aplicado) |
| `perfil_categoria` | `perfil_id`, `categoria_id`, `subtemas_ids` (`uuid[]`, opcional), `prioridade` (1–5, padrão 3) |
| `tema_perfil` | `id`, `perfil_id`, `tipo` (`especifico`/`auxiliar`), `texto`, `categoria_id` (nullable), `prioridade` (1–5), `profundidade` (`breve`/`normal`/`detalhada`), `embedding vector(1024)`, `ativo`, `valido_a_partir_da_edicao` (RN10) |
| `sugestao_auxiliar` | `id`, `perfil_id`, `ref_tipo` (`categoria`/`subtema`), `ref_id`, `similaridade`, `sugerido_em`, `resposta` (`aceito`/`recusado`/`pendente`), `respondido_em` (RF11, RF42) |

Restrição RN07: `perfil_categoria` tem exatamente 4 linhas por perfil. A regra é validada no
serviço, porque o banco não tem como checar contagem diretamente, e coberta por teste.

### Edições e feedback

| Tabela | Colunas principais |
|--------|--------------------|
| `edicao` | `id`, `usuario_id`, `numero` (sequencial por usuário), `data_ref`, `perfil_versao`, `status` (`gerando`/`pronta`/`falhou`), `pdf_caminho`, `paginas`, `gerada_em`, `prompt_versao`, `modelo_chat` |
| `materia` | `id`, `edicao_id`, `ordem`, `secao` (nome da categoria, "Destaque" ou "Sugestões"), `tipo` (`principal`/`normal`/`sugestao`), `tema_perfil_id` ou `categoria_id`, `titulo`, `linha_fina`, `texto`, `verificada` (bool), `motivo_descarte` (nullable) |
| `materia_fonte` | `materia_id`, `item_id`, `papel` (`base`/`relacionado`), `status_relacionado` (nullable, RF22) (RF23) |
| `avaliacao` | `id`, `edicao_id`, `nota` (1–5), `comentario`, `novo_topico`, `secoes_avaliadas` (`jsonb`: `{secao: "gostei"/"neutro"/"nao_gostei"}`), `criada_em` |
| `ajuste_perfil` | `id`, `avaliacao_id`, `tipo` (`adicionar_tema`/`remover_tema`/`mudar_prioridade`/`mudar_profundidade`/`trocar_categoria`), `payload` (`jsonb`), `justificativa`, `status` (`proposto`/`confirmado`/`rejeitado`), `aplicado_na_versao` (RF39–RF41) |
| `ciclo_execucao` | `id`, `usuario_id`, `iniciado_em`, `terminado_em`, `etapa_atual`, `status`, `erro`, `itens_coletados`, `trechos_indexados`, `edicao_id` |

Matérias descartadas pelo verificador (RN01) são gravadas com `verificada = false` e
`motivo_descarte`. Elas não aparecem no PDF, mas ficam disponíveis para a análise do teste de usuário.

## Critérios de aceite

- **CA-01.1** `dotnet ef database update` cria o schema completo, com a extensão `vector` e o índice HNSW.
- **CA-01.2** Uma consulta de similaridade com filtro `publicado_em >= now() - 24h` e `categoria_id IN (...)` usa o índice (verificado com `EXPLAIN`).
