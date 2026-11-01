# 03 · Perfil e interesses

Cobre: RF01–RF11, RN07.

## Fluxo de onboarding (página `/perfil/novo`)

```
[1] Consentimento ─▶ [2] Escolher 4 categorias ─▶ [3] Texto livre ─▶ [4] Revisar temas
     (RNF06)             (+ subtemas opcionais)       (RF01)           extraídos (RF02/RF03)
                                                                              │
              [7] Salvar perfil ◀─ [6] Horário (RF05) ◀─ [5] Temas auxiliares ◀┘
              + embeddings (RF06)                         aceitar/recusar (RF10/RF11)
```

### Passo 2: categorias (RF07, RN07)

- Grade com as categorias disponíveis (RN08). Cada cartão mostra o nome, a descrição e os subtemas
  como chips selecionáveis.
- O botão "Continuar" só habilita com **exatamente 4** categorias marcadas.
- Validação no servidor: `PerfilService.DefinirCategorias` lança `RegraNegocioException("RN07")` se a
  contagem for diferente de 4.

### Passo 3: texto livre (RF01)

Campo de texto com exemplo de placeholder: *"Quero acompanhar o concurso do Banco Central, novidades
do .NET e como anda a reforma tributária."* O campo é opcional, porque o usuário pode ficar só com
as categorias.

### Passo 4: extração de temas (RF02, RF03, RF08)

O LLM recebe o texto livre e a lista das 4 categorias escolhidas, com o prompt
`prompts/extrair-temas.v1.md`, e responde em JSON estruturado:

```json
{
  "temas_especificos": [
    { "texto": "concurso do Banco Central", "categoria_slug": "carreira-emprego" },
    { "texto": "novidades do .NET", "categoria_slug": "tecnologia-ia" }
  ],
  "temas_gerais": [
    { "texto": "reforma tributária", "categoria_slug": "economia-financas" }
  ]
}
```

- **Específico**: entidade, evento ou assunto nomeado e restrito, que pode virar manchete. **Geral**:
  área ampla que cabe numa seção.
- Um tema geral que já coincide com uma categoria escolhida é descartado, porque a categoria já cobre
  o assunto. Os demais temas gerais viram subtema livre da categoria mais próxima.
- `categoria_slug` pode ser `null` quando o tema não cai em nenhuma das 4. O tema continua valendo e
  é recuperado no acervo todo, sem filtro de categoria. Temas específicos não contam no limite de 4 (RN07).
- **RF03**: a tela mostra a lista editável. O usuário pode renomear, excluir, adicionar e trocar o tipo
  (específico ↔ geral) antes de salvar. Nada é salvo sem confirmação.
- Se o LLM falhar ou devolver JSON inválido, o sistema tenta mais uma vez. Se falhar de novo, cada
  linha ou frase do texto vira um tema específico candidato, para o usuário editar.

### Passo 5: temas auxiliares (RF10, RF11)

1. Monta o conjunto de consulta: os embeddings das 4 categorias e dos temas específicos confirmados.
2. Para cada vetor de consulta, busca as 3 categorias ou subtemas do catálogo mais próximos, **excluindo**
   o que já foi escolhido.
3. Junta os resultados, ordena pela maior similaridade e fica com os **5 primeiros** acima de 0.50.
4. Mostra cada sugestão com o motivo ("parecido com *Tecnologia e IA*") e botões Aceitar e Recusar.
5. Cada resposta grava uma linha em `sugestao_auxiliar` (RF11, RF42). Os aceitos viram `tema_perfil`
   do tipo `auxiliar`, com prioridade 1.

Os auxiliares aceitos só alimentam o espaço limitado de sugestões da edição (RF30). Eles não ocupam
seções nem manchetes.

### Passo 6: horário (RF05)

Seletor de hora (HH:mm), padrão 06:30, fuso `America/Sao_Paulo`. O horário define quando o ciclo
**começa**. O PDF fica pronto alguns minutos depois (spec 07).

### Passo 7: salvar (RF06)

- Gera o embedding de cada `tema_perfil`. Para dar contexto ao vetor, o texto embutido é
  `"{texto} ({nome da categoria})"` quando o tema tem categoria.
- O perfil é criado com `versao = 1`.

## Prioridades (RF04)

| Origem | Prioridade inicial | Onde aparece |
|--------|--------------------|--------------|
| Tema específico | 5 | Manchetes, incluindo a principal |
| Categoria escolhida | 3 | Seções |
| Subtema escolhido | 3 (reforça a categoria na consulta) | Seções |
| Tema auxiliar aceito | 1 | Só no bloco de sugestões |

O feedback (spec 08) ajusta essas prioridades dentro do intervalo de 1 a 5.

## Editar perfil depois (`/perfil`)

Mesmas telas, com os dados preenchidos. A troca de categorias continua exigindo exatamente 4 (RN07).
Toda alteração incrementa `perfil.versao` e só vale para a próxima edição.

## Critérios de aceite

- **CA-03.1** Não é possível salvar um perfil com 3 ou 5 categorias, nem pela UI nem pelo serviço.
- **CA-03.2** Com um `IChatClient` fake que devolve o JSON de exemplo, a tela de revisão mostra 2 específicos e 1 geral, e a edição do usuário é o que fica salvo.
- **CA-03.3** As sugestões auxiliares nunca incluem categorias já escolhidas, e cada resposta fica registrada com data.
- **CA-03.4** Todos os `tema_perfil` ativos têm embedding não nulo após salvar.
