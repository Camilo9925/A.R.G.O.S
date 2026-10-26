using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    neighborhood_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    street = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source_resource_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    source_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_simulated = table.Column<bool>(type: "boolean", nullable: false),
                    author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "fk_occurrences_neighborhoods_neighborhood_id",
                        column: x => x.neighborhood_id,
                        principalTable: "neighborhoods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_occurrences_neighborhood_id",
                table: "occurrences",
                column: "neighborhood_id");

            migrationBuilder.CreateIndex(
                name: "ix_occurrences_occurred_at",
                table: "occurrences",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occurrences");
        }
    }
}
