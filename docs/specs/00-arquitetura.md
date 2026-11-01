# 00 · Arquitetura e decisões

## Visão do pipeline

```
 ┌─────────────┐   ┌──────────────┐   ┌──────────────┐   ┌─────────────┐   ┌──────────┐
 │ Coleta RSS  │──▶│  Indexação   │──▶│ Recuperação  │──▶│  Geração    │──▶│ PDF A4   │
 │ (Syndication│   │ chunk+embed  │   │ por tema,24h │   │ LLM + few-  │   │ QuestPDF │
 │  + validador│   │ → pgvector   │   │ + agrupamento│   │ shot + veri-│   │          │
 └─────────────┘   └──────────────┘   │ + relacionad.│   │ ficador     │   └────┬─────┘
                                      └──────────────┘   └─────────────┘        │
        ▲                                                                       ▼
 ┌──────┴──────────────────────────────────────────────┐              ┌────────────────┐
 │ Perfil (temas gerais, específicos, auxiliares)      │◀── ajustes ──│ Feedback (1–5, │
 │ embeddings dos temas · horário · histórico          │  confirmados │ comentário,    │
 └─────────────────────────────────────────────────────┘              │ novo tópico)   │
                                                                      └────────────────┘
```

## Stack

| Camada | Escolha | Motivo |
|--------|---------|--------|
| Runtime | .NET 10 (SDK 10.0.401 já instalado) | LTS atual |
| Web/UI | Blazor Server (Interactive Server) | Uma só linguagem; telas simples de perfil e feedback |
| Banco relacional + vetorial | PostgreSQL 17 + pgvector (Docker) | ✅ **Confirmado em 01/11/2026.** Um banco só guarda o perfil, o histórico e os vetores, e permite filtrar por data e categoria no mesmo SQL. Qdrant exigiria um segundo armazenamento para os dados relacionais. Ver "Alternativas avaliadas" abaixo. |
| Acesso a dados | EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` + `Pgvector.EntityFrameworkCore` | Migrations e consultas vetoriais tipadas |
| Abstração de IA | `Microsoft.Extensions.AI` (`IChatClient`, `IEmbeddingGenerator<string, Embedding<float>>`) | Permite trocar Ollama ↔ API por configuração |
| Embeddings | Ollama local, modelo `bge-m3` (1024 dims, multilíngue) | ⚠ Decisão proposta. Bom em português, roda local e não tem custo por chamada. |
| Modelo de chat | Configurável. Padrão: Ollama `qwen2.5:7b-instruct`; alternativa: API externa | ⚠ Decisão proposta. Ver "Modelo de linguagem" abaixo. |
| Feeds | `System.ServiceModel.Syndication` | Indicado no levantamento; lê RSS 2.0 e Atom |
| PDF | QuestPDF (Community License) | ⚠ Decisão proposta. API fluente em C#, layout em colunas, gratuito para uso individual e acadêmico. |
| QR code | QRCoder | Gera PNG em memória, sem dependências |
| Agendamento | `BackgroundService` com timer diário | ⚠ Decisão proposta. Um usuário e um horário não justificam o Quartz.NET. |
| Testes | xUnit + Testcontainers (Postgres) + fakes de `IChatClient`/`IEmbeddingGenerator` | Testes determinísticos sem LLM |

### Banco de dados: alternativas avaliadas (decisão fechada)

| Opção | Por que não |
|-------|-------------|
| **Qdrant** + PostgreSQL | Dois armazenamentos para sincronizar. O filtro por data e categoria da spec 05 deixaria de ser um `WHERE` no mesmo SQL da busca vetorial. |
| **SQL Server 2025** | Tecnicamente viável: tipo `VECTOR(n)` e `VECTOR_DISTANCE` são GA desde nov/2025, suportam até 1.998 dimensões (os 1024 do bge-m3 cabem) e o EF Core 10 tem suporte nativo via `SqlVector<float>`. Descartado por custo de migração, não por capacidade: a instância local é a 2022 (16.0), que não tem tipo vetorial, e o índice vetorial (DiskANN) ainda é *preview* no 2025. Além disso, a spec 01 teria de ser reescrita em T-SQL (`jsonb` → `json`, `uuid[]` → tabela de junção, `timestamptz` → `datetimeoffset`, HNSW → kNN exato). |
| **SQLite + sqlite-vec** | Dispensaria o Docker, mas perde `jsonb`, `uuid[]`, `timestamptz` e HNSW maduro — vários campos da spec 01 precisariam mudar. |

Nota de escala: no MVP o acervo fica na ordem de alguns milhares de trechos, volume em que
busca exata e índice aproximado empatam. O HNSW entra pensando no acervo cumulativo (RF16),
que cresce ~1.000 itens/dia.

### Modelo de linguagem (⚠ decisão proposta)

- **Embeddings sempre locais** (Ollama `bge-m3`). Trocar o modelo de embedding exige reindexar o
  acervo, então ele precisa ficar estável.
- **Chat configurável** em `Jornal:Ai:Chat:Provider` (`Ollama` | `OpenAICompatible`). O padrão é Ollama.
  Se a qualidade das manchetes com humor (RF26) ficar fraca num modelo 7B, mudamos só o provider
  de chat, sem tocar no resto.
- A comparação "com e sem RAG" da demo (spec 09) usa o mesmo `IChatClient`, só sem os trechos.

## Estrutura da solução

```
ProjectDonsJornal/
├─ docker-compose.yml          # postgres+pgvector, ollama
├─ Jornal.slnx                 # formato novo de solução do .NET 10
├─ dotnet-tools.json           # dotnet-ef como ferramenta local, versionada
├─ src/
│  ├─ Jornal.Core/             # domínio, interfaces, serviços de aplicação (sem I/O)
│  │  ├─ Comum/  Catalogo/  Perfis/  Coleta/  Recuperacao/  Geracao/  Feedback/  Ciclo/
│  │  │  # Comum/: tipos usados por mais de uma área (Profundidade, TipoMateria, options).
│  │  │  # Perfis/ no plural: a classe Perfil e um namespace Perfil colidiriam em C#.
│  ├─ Jornal.Infrastructure/   # EF Core, pgvector, leitor de feeds, clientes de IA, QuestPDF
│  ├─ Jornal.Web/              # Blazor: onboarding, perfil, edições, feedback + host do agendador
│  └─ Jornal.Cli/              # comandos: validar-feeds, coletar, gerar, seed, comparar-rag
├─ prompts/                    # templates de prompt versionados (RF28)
│  ├─ manchetes.v1.md
│  ├─ verificador.v1.md
│  ├─ extrair-temas.v1.md
│  └─ interpretar-feedback.v1.md
├─ data/
│  ├─ catalogo.json            # categorias, subtemas, descrições e feeds candidatos
│  └─ edicoes/                 # PDFs gerados (entrega, ver spec 07)
├─ tests/
│  ├─ Jornal.Core.Tests/
│  └─ Jornal.Integration.Tests/
└─ docs/specs/
```

Regra de dependência: `Core` não referencia nada de infraestrutura. `Infrastructure` implementa as
interfaces do `Core`. `Web` e `Cli` são composição (DI) e interface.

## Interfaces centrais (Core)

```csharp
public interface IFeedReader      { Task<FeedReadResult> ReadAsync(Feed feed, CancellationToken ct); }
public interface IFeedValidator   { Task<FeedValidationResult> ValidateAsync(string url, CancellationToken ct); }
public interface IColetaService   { Task<ColetaResumo> ColetarAsync(CancellationToken ct); }
public interface IIndexador       { Task<int> IndexarPendentesAsync(CancellationToken ct); }
public interface IRecuperador     { Task<PautaDaEdicao> MontarPautaAsync(Perfil perfil, DateTimeOffset agora, CancellationToken ct); }
public interface IRedator         { Task<EdicaoRascunho> RedigirAsync(PautaDaEdicao pauta, CancellationToken ct); }
public interface IVerificador     { Task<VerificacaoResultado> VerificarAsync(MateriaRascunho m, IReadOnlyList<Trecho> base_, CancellationToken ct); }
public interface IEdicaoRenderer  { Task<byte[]> RenderPdfAsync(Edicao edicao, CancellationToken ct); }
public interface IEntregaEdicao   { Task EntregarAsync(Edicao edicao, byte[] pdf, CancellationToken ct); }
public interface ICicloDiario     { Task<ResultadoCiclo> ExecutarAsync(Guid usuarioId, CancellationToken ct); }
public interface IInterpretadorFeedback { Task<IReadOnlyList<AjustePerfilProposto>> InterpretarAsync(Avaliacao a, Perfil p, CancellationToken ct); }
```

## Parâmetros configuráveis

Seção `Jornal` do `appsettings.json`. Os valores iniciais serão calibrados no marco M3 (spec 09).

| Chave | Padrão | Uso |
|-------|--------|-----|
| `Recuperacao:JanelaHoras` | 24 | RF19 |
| `Recuperacao:LimiarSimilaridade` | 0.55 | RN03, similaridade de cosseno mínima entre o tema e o trecho |
| `Recuperacao:TopKPorTema` | 12 | Candidatos por tema antes do agrupamento |
| `Recuperacao:LimiarMesmoFato` | 0.82 | RF20, agrupamento de itens no mesmo assunto |
| `Recuperacao:RelacionadosDias` | 30 | RF21 |
| `Recuperacao:RelacionadosMax` | 2 | RF21 |
| `Coleta:LimiarDuplicata` | 0.93 | RF15, conteúdo muito similar |
| `Coleta:DuplicataJanelaHoras` | 72 | Janela de comparação para duplicatas |
| `Coleta:TimeoutSegundos` | 20 | Tempo limite por feed |
| `Edicao:MaxMaterias` | 8 | Total de matérias, sem contar as sugestões |
| `Edicao:MaxSugestoes` | 2 | RF30 |
| `Edicao:MaxPaginas` | 2 | RF32, RNF05 |
| `Ai:Chat:Provider` / `Model` / `Endpoint` | Ollama / qwen2.5:7b-instruct / http://localhost:11434 | |
| `Ai:Embedding:Model` | bge-m3 | |
| `Entrega:Pasta` | `data/edicoes` | RF35 |
| `Prompts:Manchetes` | `prompts/manchetes.v1.md` | RF28 |

## Decisões em aberto: situação

| Decisão | Proposta | Status |
|---------|----------|--------|
| Entrega do PDF | Pasta local + link na página web (spec 07) | ⚠ proposta |
| Interface de perfil e feedback | Página web simples em Blazor | ⚠ proposta |
| Vector store | PostgreSQL + pgvector | ✅ confirmado em 01/11/2026 (M0 entregue) |
| Biblioteca de PDF | QuestPDF | ⚠ proposta |
| Modelo de linguagem | Embeddings via Ollama; chat configurável, padrão Ollama | ⚠ proposta |
| Limiar (RN03) e itens por edição | 0.55; 8 matérias + 2 sugestões | a calibrar no M3 |
| Lista final de feeds | Sai do comando `validar-feeds` (spec 02) | a executar no M1 |
