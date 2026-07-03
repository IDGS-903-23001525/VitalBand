using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VitalBand.Migrations
{
    /// <inheritdoc />
    public partial class FirstMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "USUARIOS",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    email = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rol = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_registro = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    token_sesion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sesion_expiracion = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIOS", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MEDICOS",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    especialidad = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cedula_profesional = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEDICOS", x => x.id);
                    table.ForeignKey(
                        name: "FK_MEDICOS_USUARIOS_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "USUARIOS",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PACIENTES",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    medico_asignado_id = table.Column<int>(type: "int", nullable: true),
                    nombre = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    genero = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha_nacimiento = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    tipo_sangre = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    peso_inicial = table.Column<float>(type: "float", nullable: true),
                    altura_inicial = table.Column<float>(type: "float", nullable: true),
                    historial_medico_breve = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PACIENTES", x => x.id);
                    table.ForeignKey(
                        name: "FK_PACIENTES_MEDICOS_medico_asignado_id",
                        column: x => x.medico_asignado_id,
                        principalTable: "MEDICOS",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_PACIENTES_USUARIOS_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "USUARIOS",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ALERTAS",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    paciente_id = table.Column<int>(type: "int", nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    fc_media = table.Column<float>(type: "float", nullable: false),
                    hrv_rmssd = table.Column<float>(type: "float", nullable: false),
                    spo2_estabilidad = table.Column<float>(type: "float", nullable: false),
                    latitud = table.Column<float>(type: "float", nullable: true),
                    longitud = table.Column<float>(type: "float", nullable: true),
                    mensaje_enviado = table.Column<bool>(type: "tinyint(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ALERTAS", x => x.id);
                    table.ForeignKey(
                        name: "FK_ALERTAS_PACIENTES_paciente_id",
                        column: x => x.paciente_id,
                        principalTable: "PACIENTES",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ALERTAS_paciente_id",
                table: "ALERTAS",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_MEDICOS_usuario_id",
                table: "MEDICOS",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_PACIENTES_medico_asignado_id",
                table: "PACIENTES",
                column: "medico_asignado_id");

            migrationBuilder.CreateIndex(
                name: "IX_PACIENTES_usuario_id",
                table: "PACIENTES",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ALERTAS");

            migrationBuilder.DropTable(
                name: "PACIENTES");

            migrationBuilder.DropTable(
                name: "MEDICOS");

            migrationBuilder.DropTable(
                name: "USUARIOS");
        }
    }
}
