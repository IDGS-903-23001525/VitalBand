using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VitalBand.Migrations
{
    /// <inheritdoc />
    public partial class AddPacienteRegistrationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CONTACTOS_EMERGENCIA",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    paciente_id = table.Column<int>(type: "int", nullable: false),
                    nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    parentesco = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telefono = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    prioridad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTACTOS_EMERGENCIA", x => x.id);
                    table.ForeignKey(
                        name: "FK_CONTACTOS_EMERGENCIA_PACIENTES_paciente_id",
                        column: x => x.paciente_id,
                        principalTable: "PACIENTES",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PATOLOGIAS_CATALOGO",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre_enfermedad = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    descripcion = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PATOLOGIAS_CATALOGO", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PACIENTES_PATOLOGIAS",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    paciente_id = table.Column<int>(type: "int", nullable: false),
                    patologia_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PACIENTES_PATOLOGIAS", x => x.id);
                    table.ForeignKey(
                        name: "FK_PACIENTES_PATOLOGIAS_PACIENTES_paciente_id",
                        column: x => x.paciente_id,
                        principalTable: "PACIENTES",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PACIENTES_PATOLOGIAS_PATOLOGIAS_CATALOGO_patologia_id",
                        column: x => x.patologia_id,
                        principalTable: "PATOLOGIAS_CATALOGO",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CONTACTOS_EMERGENCIA_paciente_id",
                table: "CONTACTOS_EMERGENCIA",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_PACIENTES_PATOLOGIAS_paciente_id",
                table: "PACIENTES_PATOLOGIAS",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "IX_PACIENTES_PATOLOGIAS_patologia_id",
                table: "PACIENTES_PATOLOGIAS",
                column: "patologia_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CONTACTOS_EMERGENCIA");

            migrationBuilder.DropTable(
                name: "PACIENTES_PATOLOGIAS");

            migrationBuilder.DropTable(
                name: "PATOLOGIAS_CATALOGO");
        }
    }
}
