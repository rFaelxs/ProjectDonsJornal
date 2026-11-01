# Levantamento de feeds (M1)

Execução do validador da [spec 02](specs/02-catalogo-e-feeds.md) em **01/11/2026**, com o comando:

```bash
jornal validar-url <url> [<url> ...]
```

Foram testados **32 candidatos** nas 4 categorias da demonstração (spec 09). **18 passaram** o
validador e **16 entraram no catálogo** — dois aprovados ficaram de fora por serem feeds genéricos
(ver o ponto 4 abaixo). Nenhuma URL entrou no `catalogo.json` sem passar pelos 4 passos.

## Aprovados

### Tecnologia e IA — 5 feeds

| Veículo | URL | Formato | Itens recentes |
|---------|-----|---------|----------------|
| Canaltech | `https://canaltech.com.br/rss/` | rss | sim |
| Tecnoblog | `https://tecnoblog.net/feed/` | rss | sim |
| Meio Bit | `https://meiobit.com/feed/` | rss | sim |
| Olhar Digital | `https://olhardigital.com.br/feed/` | rss | sim |
| G1 Tecnologia | `https://g1.globo.com/rss/g1/tecnologia/` | rss | sim |

### Carreira e emprego — 4 feeds

| Veículo | URL | Formato | Itens recentes |
|---------|-----|---------|----------------|
| Estratégia Concursos | `https://www.estrategiaconcursos.com.br/blog/feed/` | rss | 10/10 |
| Gran Cursos Online | `https://blog.grancursosonline.com.br/feed/` | rss | 30/30 |
| JC Concursos | `https://jcconcursos.com.br/feed` | rss | 30/30 |
| Concursos no Brasil | `https://www.concursosnobrasil.com.br/feed/` | rss | 15/15 |

### Economia e finanças — 4 feeds

| Veículo | URL | Formato | Itens recentes |
|---------|-----|---------|----------------|
| InfoMoney | `https://www.infomoney.com.br/feed/` | rss | 10/10 |
| Agência Brasil Economia | `https://agenciabrasil.ebc.com.br/rss/economia/feed.xml` | rss | 10/10 |
| G1 Economia | `https://g1.globo.com/rss/g1/economia/` | rss | 100/100 |
| Seu Dinheiro | `https://www.seudinheiro.com/feed/` | rss | 10/10 |

### Esportes — 3 feeds

| Veículo | URL | Formato | Itens recentes |
|---------|-----|---------|----------------|
| GE | `https://ge.globo.com/rss/ge/` | rss | 100/100 |
| Agência Brasil Esportes | `https://agenciabrasil.ebc.com.br/rss/esportes/feed.xml` | rss | 10/10 |
| Gazeta Esportiva | `https://www.gazetaesportiva.com/feed/` | rss | 50/50 |

**RN08 atendido**: todas as 4 categorias ficam com 3 ou mais feeds ativos, acima do mínimo de 2.

## Reprovados

| URL | Motivo | Leitura |
|-----|--------|---------|
| `https://exame.com/carreira/feed/` | `http_404` | Feed por seção não existe; só o geral (`/feed/`) responde |
| `https://exame.com/economia/feed/` | `http_404` | idem |
| `https://www.pciconcursos.com.br/noticias/feed` | `http_404` | Sem RSS público |
| `https://www.lance.com.br/feed` | `http_404` | Sem RSS público |
| `https://www.qconcursos.com/blog/feed` | `http_403` | Bloqueia o User-Agent do leitor |
| `https://www.qconcursos.com/noticias/feed` | `formato_invalido` | Responde 200 com HTML |
| `https://www.direcaoconcursos.com.br/artigos/feed/` | `formato_invalido` | Responde 200 com HTML |
| `https://www.folhadirigida.com.br/noticias/feed` | `formato_invalido` | Responde 200 com HTML |
| `https://www.folha.uol.com.br/mercado/rss091.xml` | `http_301` | Redireciona para `http://` (downgrade) e cai numa página HTML |
| `https://g1.globo.com/rss/g1/concursos-e-emprego/` | `sem_itens_recentes` | Feed existe e responde 200, mas está parado |
| `https://g1.globo.com/rss/g1/economia/concursos-e-emprego/` | `sem_itens_recentes` | idem |
| `https://rss.uol.com.br/feed/esporte.xml` | `sem_itens_recentes` | Feed parado |
| `https://sportv.globo.com/rss/sportv/` | `sem_itens_recentes` | Feed parado |
| `https://blog.tecconcursos.com.br/feed/` | `timeout` | Não respondeu em 20 s |

## O que o levantamento ensinou

**1. Feed que responde 200 não é feed que funciona.** Cinco candidatos devolveram 200 com HTML
e quatro devolveram RSS legítimo mas parado há semanas. Os passos 2 e 3 do validador — formato e
itens recentes — reprovaram todos. Sem eles, esses feeds entrariam no catálogo e só dariam
problema no M2, durante a coleta.

**2. O `System.ServiceModel.Syndication` é estrito demais para a web brasileira.** O feed de
esportes do UOL derruba o `Rss20Serializer` com `NotSupportedException: does not support RSS
version ''`. Outros usam `pubDate` como `2026-11-01 10:00:00 BRT`, que não é RFC 822. Por isso o
`LeitorSindicacao` tem dois caminhos: tenta o Syndication e, quando ele recusa, cai para um leitor
com `XDocument` que analisa a data com tolerância. Isto está coberto pelo teste
`Aprova_feed_que_o_syndication_recusa_usando_o_leitor_tolerante`.

**3. Redirect para `http://` não é seguido, e está certo.** O `HttpClient` recusa downgrade de
HTTPS para HTTP por padrão. A URL da Folha cai nesse caso — e o destino final nem é feed. O
validador reporta `http_301`, que é a resposta honesta.

**4. Categorias temáticas exigem feeds temáticos.** Agência Brasil "geral" e Exame "geral" passam
o validador, mas alimentariam `carreira-emprego` com notícia de qualquer assunto. Como a
recuperação filtra por `categoria_id` (spec 05, passo 1), isso contaminaria a seção. Ficaram de
fora em favor de 4 feeds específicos de concursos.

## Risco que permanece

Dos 16 escolhidos, **3 são do grupo Globo** (G1 Tecnologia, G1 Economia, GE) e **2 da EBC**
(Agência Brasil Economia e Esportes). Uma mudança de política de RSS nesses dois grupos derrubaria
feeds em 3 das 4 categorias ao mesmo tempo. A mitigação é o próprio validador no ciclo diário: 3 falhas
consecutivas desativam o feed e a página de administração avisa (spec 02).

Revalidar com `jornal validar-feeds --atualizar` na véspera da apresentação.
