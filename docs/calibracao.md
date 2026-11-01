# Calibração dos limiares

A spec 09 manda anotar aqui os valores escolhidos e a justificativa, a partir de dados reais.
Este documento começa com a **primeira medição real**, feita no fim do M2.

## Medição 1 — 01/11/2026

**Acervo**: 295 itens, 294 trechos, coletados numa execução única de `jornal coletar` sobre os
16 feeds ativos. Modelo de embedding: `bge-m3` (1024 dimensões).

> Ressalva: é **um dia** de coleta. A calibração definitiva é do M3, com vários dias acumulados
> e com os temas reais do perfil da demo. O que está abaixo é sinal forte, não conclusão.

### O problema: `LimiarSimilaridade = 0.55` zeraria toda edição

Similaridade de cosseno entre o vetor de consulta e o trecho mais próximo das últimas 24 h:

| Consulta | Melhor similaridade | Resultado no topo | Passa em 0.55? |
|---|---|---|---|
| Categoria "Tecnologia e IA" (descrição completa) | **0,539** | "O que são parâmetros de modelos de IA?" — correto | ❌ |
| Tema "inteligência artificial e novos modelos de IA" | **0,481** | "O que são parâmetros de modelos de IA?" — correto | ❌ |
| Tema "concurso público, edital e inscrições" | **0,471** | "Gabarito Detran SP extraoficial" — correto | ❌ |
| Tema "cibersegurança, vazamento de dados e ataques hacker" | **0,377** | "Suspeita de vazamento da Bola de Ouro" — **falso positivo** | ❌ |

Com o limiar proposto de 0,55, **nenhuma dessas consultas produziria material**. Todos os temas
cairiam em `Omitidos` com motivo `abaixo_do_limiar` e a edição sairia vazia — só com a marca de
fim (RF32).

### Por que o 0,55 era alto

O `bge-m3` comprime as similaridades de texto curto em português numa faixa estreita, na prática
entre 0,33 e 0,55. O valor 0,55 da spec 00 foi uma proposta a calibrar, e correspondia melhor à
escala de outros modelos de embedding, onde o espalhamento é maior.

Vale notar a assimetria: a consulta por **descrição de categoria** (0,539) pontua mais alto que a
consulta por **tema específico curto** (0,481), porque a descrição é um texto mais longo e mais
parecido em forma com o trecho. Isso sugere que categoria e tema específico talvez precisem de
limiares diferentes — a decidir no M3.

### Proposta: `LimiarSimilaridade = 0.43`

O que esse valor faz com os dados medidos:

- ✅ aceita "inteligência artificial" (0,481) e "concurso público" (0,471), ambos com resultado correto;
- ✅ aceita a categoria "Tecnologia e IA" (0,539);
- ✅ **rejeita** o falso positivo de "cibersegurança" (0,377), que trouxe notícia de futebol;
- deixa uma margem de 0,04 acima do falso positivo e de 0,04 abaixo do verdadeiro positivo mais fraco.

Não foi aplicado ainda no `appsettings.json`: a spec 09 coloca a calibração no M3, com o comando
`jornal calibrar --tema "<texto>"` e com mais dias de acervo. Trocar agora, com uma amostra de um
dia, seria trocar um palpite por outro.

### Os outros limiares

| Parâmetro | Proposto | Situação |
|---|---|---|
| `LimiarDuplicata` (0.93) | 0,93 | **Parece bom.** Em 295 itens achou 1 duplicata, e ela é real: a Gazeta Esportiva publicou o mesmo texto em dois links. Nenhum falso positivo. |
| `LimiarMesmoFato` (0.82) | 0,82 | **Não medido.** Precisa de um fato coberto por várias fontes no mesmo dia, que esta amostra não tem de forma clara. |
| `LimiarRelacionados` (0.60) | 0,60 | **Não medido.** Precisa de acervo com 30 dias (RF21). Provavelmente alto demais pelo mesmo motivo do 0,55. |

## O que medir no M3

1. Rodar `jornal calibrar --tema "<texto>"` com os 2 ou 3 temas específicos reais do perfil da demo.
2. Repetir com 7 dias de acervo, para ver se a faixa de similaridade se mantém.
3. Verificar se categoria e tema específico precisam de limiares separados.
4. Encontrar um fato com cobertura em 3+ fontes e medir o agrupamento, para fechar `LimiarMesmoFato`.
