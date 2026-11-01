using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Jornal.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "categoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    embedding_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    apelido = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    consentimento_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feed",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_veiculo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    validado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultima_coleta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    falhas_consecutivas = table.Column<int>(type: "integer", nullable: false),
                    e_tag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ultimo_modificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feed", x => x.id);
                    table.ForeignKey(
                        name: "fk_feed_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subtema",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    embedding_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subtema", x => x.id);
                    table.ForeignKey(
                        name: "fk_subtema_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "edicao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    data_ref = table.Column<DateOnly>(type: "date", nullable: false),
                    perfil_versao = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pdf_caminho = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    paginas = table.Column<int>(type: "integer", nullable: false),
                    gerada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    prompt_versao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    modelo_chat = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_edicao", x => x.id);
                    table.ForeignKey(
                        name: "fk_edicao_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "perfil",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto_livre_original = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    horario_geracao = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    fuso = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfil", x => x.id);
                    table.ForeignKey(
                        name: "fk_perfil_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feed_validacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    executado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    formato = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    itens_recentes = table.Column<int>(type: "integer", nullable: false),
                    aprovado = table.Column<bool>(type: "boolean", nullable: false),
                    motivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feed_validacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_feed_validacao_feed_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feed",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    titulo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    resumo = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    link_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_estimada = table.Column<bool>(type: "boolean", nullable: false),
                    coletado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duplicata_de = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_item_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_item_feed_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feed",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_item_item_duplicata_de",
                        column: x => x.duplicata_de,
                        principalTable: "item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avaliacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edicao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nota = table.Column<int>(type: "integer", nullable: false),
                    comentario = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    novo_topico = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    secoes_avaliadas = table.Column<string>(type: "jsonb", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avaliacao", x => x.id);
                    table.CheckConstraint("ck_avaliacao_nota", "nota BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_avaliacao_edicoes_edicao_id",
                        column: x => x.edicao_id,
                        principalTable: "edicao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ciclo_execucao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    terminado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    etapa_atual = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    erro = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    itens_coletados = table.Column<int>(type: "integer", nullable: false),
                    trechos_indexados = table.Column<int>(type: "integer", nullable: false),
                    edicao_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ciclo_execucao", x => x.id);
                    table.ForeignKey(
                        name: "fk_ciclo_execucao_edicoes_edicao_id",
                        column: x => x.edicao_id,
                        principalTable: "edicao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ciclo_execucao_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "perfil_categoria",
                columns: table => new
                {
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subtemas_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    prioridade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfil_categoria", x => new { x.perfil_id, x.categoria_id });
                    table.CheckConstraint("ck_perfil_categoria_prioridade", "prioridade BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_perfil_categoria_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_perfil_categoria_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sugestao_auxiliar",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ref_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ref_id = table.Column<Guid>(type: "uuid", nullable: false),
                    similaridade = table.Column<double>(type: "double precision", nullable: false),
                    sugerido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resposta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    respondido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sugestao_auxiliar", x => x.id);
                    table.ForeignKey(
                        name: "fk_sugestao_auxiliar_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tema_perfil",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prioridade = table.Column<int>(type: "integer", nullable: false),
                    profundidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    valido_a_partir_da_edicao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tema_perfil", x => x.id);
                    table.CheckConstraint("ck_tema_perfil_prioridade", "prioridade BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_tema_perfil_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tema_perfil_perfil_perfil_id",
                        column: x => x.perfil_id,
                        principalTable: "perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trecho",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fonte = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trecho", x => x.id);
                    table.ForeignKey(
                        name: "fk_trecho_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trecho_item_item_id",
                        column: x => x.item_id,
                        principalTable: "item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ajuste_perfil",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    avaliacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    aplicado_na_versao = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ajuste_perfil", x => x.id);
                    table.ForeignKey(
                        name: "fk_ajuste_perfil_avaliacoes_avaliacao_id",
                        column: x => x.avaliacao_id,
                        principalTable: "avaliacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "coleta_falha",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ocorreu_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coleta_falha", x => x.id);
                    table.ForeignKey(
                        name: "fk_coleta_falha_ciclo_execucao_ciclo_id",
                        column: x => x.ciclo_id,
                        principalTable: "ciclo_execucao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_coleta_falha_feed_feed_id",
                        column: x => x.feed_id,
                        principalTable: "feed",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "materia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    edicao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    secao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tema_perfil_id = table.Column<Guid>(type: "uuid", nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    linha_fina = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    texto = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    verificada = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_descarte = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materia", x => x.id);
                    table.ForeignKey(
                        name: "fk_materia_categoria_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categoria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_materia_edicao_edicao_id",
                        column: x => x.edicao_id,
                        principalTable: "edicao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_materia_temas_perfil_tema_perfil_id",
                        column: x => x.tema_perfil_id,
                        principalTable: "tema_perfil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "materia_fonte",
                columns: table => new
                {
                    materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    papel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    frase = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status_relacionado = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materia_fonte", x => new { x.materia_id, x.item_id, x.papel });
                    table.ForeignKey(
                        name: "fk_materia_fonte_item_item_id",
                        column: x => x.item_id,
                        principalTable: "item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_materia_fonte_materia_materia_id",
                        column: x => x.materia_id,
                        principalTable: "materia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ajuste_perfil_avaliacao_id_status",
                table: "ajuste_perfil",
                columns: new[] { "avaliacao_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_avaliacao_edicao_id",
                table: "avaliacao",
                column: "edicao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categoria_slug",
                table: "categoria",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ciclo_execucao_edicao_id",
                table: "ciclo_execucao",
                column: "edicao_id");

            migrationBuilder.CreateIndex(
                name: "ix_ciclo_execucao_usuario_id_iniciado_em",
                table: "ciclo_execucao",
                columns: new[] { "usuario_id", "iniciado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_coleta_falha_ciclo_id",
                table: "coleta_falha",
                column: "ciclo_id");

            migrationBuilder.CreateIndex(
                name: "ix_coleta_falha_feed_id_ocorreu_em",
                table: "coleta_falha",
                columns: new[] { "feed_id", "ocorreu_em" });

            migrationBuilder.CreateIndex(
                name: "ix_edicao_usuario_id_data_ref",
                table: "edicao",
                columns: new[] { "usuario_id", "data_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_edicao_usuario_id_numero",
                table: "edicao",
                columns: new[] { "usuario_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_feed_ativo_categoria_id",
                table: "feed",
                columns: new[] { "ativo", "categoria_id" });

            migrationBuilder.CreateIndex(
                name: "ix_feed_categoria_id",
                table: "feed",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_feed_url",
                table: "feed",
                column: "url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_feed_validacao_feed_id_executado_em",
                table: "feed_validacao",
                columns: new[] { "feed_id", "executado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_item_categoria_id",
                table: "item",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_duplicata_de",
                table: "item",
                column: "duplicata_de");

            migrationBuilder.CreateIndex(
                name: "ix_item_feed_id",
                table: "item",
                column: "feed_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_link_hash",
                table: "item",
                column: "link_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_publicado_em",
                table: "item",
                column: "publicado_em");

            migrationBuilder.CreateIndex(
                name: "ix_materia_categoria_id",
                table: "materia",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_materia_edicao_id_ordem",
                table: "materia",
                columns: new[] { "edicao_id", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ix_materia_tema_perfil_id",
                table: "materia",
                column: "tema_perfil_id");

            migrationBuilder.CreateIndex(
                name: "ix_materia_fonte_item_id",
                table: "materia_fonte",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_perfil_usuario_id",
                table: "perfil",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_perfil_categoria_categoria_id",
                table: "perfil_categoria",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_subtema_categoria_id_slug",
                table: "subtema",
                columns: new[] { "categoria_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sugestao_auxiliar_perfil_id_ref_tipo_ref_id",
                table: "sugestao_auxiliar",
                columns: new[] { "perfil_id", "ref_tipo", "ref_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tema_perfil_categoria_id",
                table: "tema_perfil",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_tema_perfil_perfil_id_ativo",
                table: "tema_perfil",
                columns: new[] { "perfil_id", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ix_trecho_categoria_id_publicado_em",
                table: "trecho",
                columns: new[] { "categoria_id", "publicado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_trecho_embedding",
                table: "trecho",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_trecho_item_id_ordem",
                table: "trecho",
                columns: new[] { "item_id", "ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trecho_publicado_em",
                table: "trecho",
                column: "publicado_em");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_apelido",
                table: "usuario",
                column: "apelido",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ajuste_perfil");

            migrationBuilder.DropTable(
                name: "coleta_falha");

            migrationBuilder.DropTable(
                name: "feed_validacao");

            migrationBuilder.DropTable(
                name: "materia_fonte");

            migrationBuilder.DropTable(
                name: "perfil_categoria");

            migrationBuilder.DropTable(
                name: "subtema");

            migrationBuilder.DropTable(
                name: "sugestao_auxiliar");

            migrationBuilder.DropTable(
                name: "trecho");

            migrationBuilder.DropTable(
                name: "avaliacao");

            migrationBuilder.DropTable(
                name: "ciclo_execucao");

            migrationBuilder.DropTable(
                name: "materia");

            migrationBuilder.DropTable(
                name: "item");

            migrationBuilder.DropTable(
                name: "edicao");

            migrationBuilder.DropTable(
                name: "tema_perfil");

            migrationBuilder.DropTable(
                name: "feed");

            migrationBuilder.DropTable(
                name: "perfil");

            migrationBuilder.DropTable(
                name: "categoria");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
