using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habitaciones.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "habitaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Piso = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActualizadaEn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrdenesActivas = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_habitaciones", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_habitaciones_Estado",
                table: "habitaciones",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_habitaciones_Numero",
                table: "habitaciones",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_habitaciones_Piso",
                table: "habitaciones",
                column: "Piso");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "habitaciones");
        }
    }
}
