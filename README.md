# Jornal Personalizado

Um jornal diário em PDF, de 1 a 2 páginas A4, montado a partir dos interesses de um leitor.
Coleta RSS, indexa em vetores, recupera por similaridade e redige com um LLM restrito aos
trechos recuperados (RAG) — o modelo nunca inventa fato, e cada matéria cita a fonte.

As especificações de implementação estão em [`docs/specs/`](docs/specs/README.md).

## Stack

| Camada | Escolha |
|--------|---------|
| Runtime | .NET 10 |
| Web/UI | Blazor Server |
| Banco relacional + vetorial | PostgreSQL 17 + pgvector |
| Acesso a dados | EF Core 10 + Npgsql + Pgvector |
| Embeddings | Ollama `bge-m3` (1024 dims) |
| Chat | Configurável; padrão Ollama `qwen2.5:7b-instruct` |
| PDF | QuestPDF |
| Testes | xUnit + Testcontainers |

## Pré-requisitos

- .NET SDK 10
- Docker Desktop (Postgres e Ollama sobem em container)
- ~6 GB de disco para os dois modelos do Ollama

## Subir o ambiente

```bash
# 1. Banco e Ollama
docker compose up -d

# 2. Modelos (uma vez; demora, baixa ~5 GB)
docker compose --profile setup run --rm ollama-pull

# 3. Schema do banco
dotnet run --project src/Jornal.Cli -- migrar

# 4. Conferir
dotnet run --project src/Jornal.Cli -- status
```

`dotnet ef database update` também funciona, se preferir a ferramenta do EF:

```bash
dotnet dotnet-ef database update --project src/Jornal.Infrastructure --startup-project src/Jornal.Infrastructure
```

## Rodar

```bash
dotnet run --project src/Jornal.Web     # onboarding, perfil, edições, feedback
dotnet run --project src/Jornal.Cli -- --help
```

## Testes

```bash
dotnet test                              # tudo
dotnet test tests/Jornal.Core.Tests      # unitários, sem banco nem LLM
dotnet test tests/Jornal.Integration.Tests   # exige Docker (sobe pgvector via Testcontainers)
```

Os testes de integração sobem um PostgreSQL próprio e descartam no fim. Não tocam no banco de
desenvolvimento.

## Estrutura

```
src/Jornal.Core/             domínio, interfaces, serviços de aplicação — sem I/O
src/Jornal.Infrastructure/   EF Core, pgvector, leitor de feeds, clientes de IA, QuestPDF
src/Jornal.Web/              Blazor + host do agendador diário
src/Jornal.Cli/              comandos: migrar, status, seed, coletar, gerar…
prompts/                     templates de prompt versionados (RF28)
data/catalogo.json           categorias, subtemas e feeds candidatos
docs/specs/                  as 10 especificações
```

Regra de dependência: `Core` não referencia nada de infraestrutura. `Infrastructure` implementa
as interfaces do `Core`. `Web` e `Cli` são só composição.

## Backup do acervo

O acervo nunca é apagado (RF16) e vive num volume Docker nomeado, que sobrevive a
`docker compose down`. Para um dump manual:

```bash
docker exec jornal-postgres pg_dump -U jornal -d jornal -Fc > data/demo/acervo.dump
docker exec -i jornal-postgres pg_restore -U jornal -d jornal --clean < data/demo/acervo.dump
```

## Marcos

| Marco | Entrega | Situação |
|-------|---------|----------|
| M0 | Solução, docker-compose, EF Core com migrations, testes rodando | ✅ |
| M1 | `catalogo.json` completo, `validar-feeds`, ≥ 2 feeds ativos por categoria da demo | |
| M2 | `coletar` + indexação + dedup, coleta diária ligada | |
| M3 | Onboarding web, `pauta`, calibração dos limiares | |
| M4 | Prompts v1, redator + verificador | |
| M5 | PDF A4, ciclo agendado, página `/edicoes` | |
| M6 | Feedback, interpretação, confirmação, histórico | |
| M7 | Demo ensaiada, comparação com e sem RAG, plano B | |

O caminho crítico é o **M2 cedo**: sem dias de acervo acumulado não existe demonstração de
conteúdos relacionados (RF21).
