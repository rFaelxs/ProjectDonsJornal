# 07 · PDF, agendamento e entrega

Cobre: RF33–RF35, RN09, RNF01–RNF05.

## Layout do PDF (QuestPDF)

| Item | Especificação | Requisito |
|------|---------------|-----------|
| Papel | A4 retrato | RF34 |
| Margens | 18 mm nas laterais, 15 mm em cima e embaixo. Espelhadas (interna 20 mm, externa 16 mm) para frente e verso | RNF02 |
| Colunas | Cabeçalho e principal em largura total. O resto em **2 colunas** com espaçamento de 8 mm | RNF02 |
| Fontes | Texto: *Source Serif 4* 10,5 pt, entrelinha 1,3. Títulos: *Source Serif 4* Bold, 15 pt (principal 24 pt). Linha fina: itálico 10,5 pt. Rodapé de fontes: 8 pt. Fontes OFL embutidas no PDF | RNF01 |
| Cor | Só preto e cinzas (≥ 40% de preto para continuar legível impresso). Nada depende de cor | RNF01 |
| Imagens | Nenhuma por padrão. QR codes opcionais em `Edicao:QrCodes = true`, monocromáticos, 14 mm | RNF03, RNF04 |
| Links | Nunca clicáveis. A fonte vem por extenso (veículo e data). Com QR ligado, cada matéria ganha um QR para o link da base principal | RNF04 |
| Páginas | 1 a 2. Numeração "1/2" no rodapé | RNF05 |
| Separadores | Filete de 0,5 pt entre matérias e filete duplo antes da marca de fim | |

A identidade visual é própria: nome, cabeçalho e tipografia não imitam nenhum jornal real (RN06).

**Pré-visualização**: o `IEdicaoRenderer` também gera um PNG da página 1, usado na página web e no
plano B da demo (spec 09).

## Ciclo diário (RF33)

`CicloDiarioService.ExecutarAsync(usuarioId)`:

```
1. cria ciclo_execucao (status=executando)
2. Coleta          → itens novos         (falha parcial OK, RF17)
3. Indexação       → trechos             (falha → aborta)
4. Recuperação     → pauta               (pauta vazia → aborta com "sem_material")
5. Geração         → matérias verificadas
6. Renderização    → PDF                 (passa de 2 páginas → corta e renderiza de novo)
7. Persistência    → edicao(status=pronta), materia, materia_fonte
8. Entrega         → arquivo + notificação
9. ciclo_execucao (status=ok)
```

Cada etapa atualiza `etapa_atual` e grava log estruturado (`ILogger` com scope `CicloId`).

### Agendador

`AgendadorEdicaoService : BackgroundService`, hospedado no `Jornal.Web`:

- A cada minuto, verifica os perfis cujo `horario_geracao` já passou hoje (no fuso do perfil) e que
  ainda não têm `edicao` com `data_ref = hoje`.
- Executa no máximo um ciclo por vez (`SemaphoreSlim(1)`).
- Se a máquina estava desligada no horário, gera assim que o serviço subir no mesmo dia (catch-up).
  Em dias anteriores, não gera nada.

Execução manual (demo e testes): `jornal gerar [--usuario <id>] [--data 2026-11-01] [--sem-coleta]`.

### Falha → última edição válida (RN09)

Se qualquer etapa de 3 a 7 falhar:

- `ciclo_execucao.status = falhou` e o erro é gravado, com exceção e etapa.
- `edicao` do dia fica com `status = falhou` e sem PDF.
- A entrega publica a **última edição com `status = pronta`**, com uma faixa no topo da página web:
  "Não foi possível gerar a edição de hoje. Mostrando a edição nº N de DD/MM." O PDF antigo não é
  alterado.

## Entrega (RF35, ⚠ decisão proposta)

`IEntregaEdicao` com duas implementações no MVP, ambas ativas:

1. **Pasta**: `data/edicoes/{usuario-apelido}/{yyyy-MM-dd}-n{numero}.pdf` e a cópia `ultima.pdf`.
2. **Página web**: `/edicoes` lista as edições (data, nº, nota dada) com os botões "Baixar PDF" e
   "Avaliar". A página inicial mostra a edição do dia.

E-mail e impressão automática ficam como implementações futuras da mesma interface.

## Critérios de aceite

- **CA-07.1** Uma pauta com 10 matérias longas gera um PDF de no máximo 2 páginas, e a principal sempre sobrevive ao corte.
- **CA-07.2** O PDF não tem anotações de link (verificado com PdfPig no teste).
- **CA-07.3** Com a geração forçada a falhar, o ciclo registra o erro, a edição do dia fica `falhou` e `/` mostra a última edição pronta com o aviso.
- **CA-07.4** O agendador não gera duas edições no mesmo dia para o mesmo usuário, mesmo reiniciando o serviço.
- **CA-07.5** Teste manual: impressão em preto e branco frente e verso com margens e leitura OK (checklist do M5).
