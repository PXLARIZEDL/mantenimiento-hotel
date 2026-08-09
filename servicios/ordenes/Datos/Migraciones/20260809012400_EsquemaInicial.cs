using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordenes.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos_procesados",
                columns: table => new
                {
                    EventoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProcesadoEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_procesados", x => x.EventoId);
                });

            migrationBuilder.CreateTable(
                name: "ordenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HabitacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HabitacionNumero = table.Column<int>(type: "integer", nullable: false),
                    TipoFalla = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Prioridad = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReportadoPor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Estado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AsignadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResueltaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TecnicoId = table.Column<Guid>(type: "uuid", nullable: true),
                    TecnicoNombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Especialidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ResueltoPor = table.Column<Guid>(type: "uuid", nullable: true),
                    NotaCierre = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_Estado",
                table: "ordenes",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_HabitacionNumero",
                table: "ordenes",
                column: "HabitacionNumero");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_procesados");

            migrationBuilder.DropTable(
                name: "ordenes");
        }
    }
}
