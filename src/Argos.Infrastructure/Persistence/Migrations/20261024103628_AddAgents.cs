using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    registration_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registration_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    search_text = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agents", x => x.id);
                    table.ForeignKey(
                        name: "fk_agents_operational_bases_base_id",
                        column: x => x.base_id,
                        principalTable: "operational_bases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    end_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_assignments_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_assignments_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_status_changes_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_assignments_equipment_id_ended_at",
                table: "agent_assignments",
                columns: new[] { "equipment_id", "ended_at" });

            migrationBuilder.CreateIndex(
                name: "ux_agent_assignments_agent_id_open",
                table: "agent_assignments",
                column: "agent_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_agent_status_changes_agent_id_occurred_at",
                table: "agent_status_changes",
                columns: new[] { "agent_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_agents_base_id",
                table: "agents",
                column: "base_id");

            migrationBuilder.CreateIndex(
                name: "ix_agents_is_active",
                table: "agents",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_agents_role",
                table: "agents",
                column: "role");

            migrationBuilder.CreateIndex(
                name: "ix_agents_status",
                table: "agents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_agents_organization_id_registration_key",
                table: "agents",
                columns: new[] { "organization_id", "registration_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_assignments");

            migrationBuilder.DropTable(
                name: "agent_status_changes");

            migrationBuilder.DropTable(
                name: "agents");
        }
    }
}
