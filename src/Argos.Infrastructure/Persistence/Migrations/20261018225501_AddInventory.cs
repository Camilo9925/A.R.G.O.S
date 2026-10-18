using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_parameters",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_parameters", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "equipment_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    icon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_built_in = table.Column<bool>(type: "boolean", nullable: false),
                    crew_assignable = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_changes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "municipalities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    state = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: false),
                    center_latitude = table.Column<double>(type: "double precision", nullable: false),
                    center_longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipalities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    acronym = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    acronym_key = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    boundary = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_type_attributes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    data_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    min = table.Column<decimal>(type: "numeric", nullable: true),
                    max = table.Column<decimal>(type: "numeric", nullable: true),
                    max_length = table.Column<int>(type: "integer", nullable: true),
                    options = table.Column<string>(type: "jsonb", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    equipment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    required_when_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    required_when_value = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_type_attributes", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_type_attributes_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_bases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    location_city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    location_neighborhood_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    location_state = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    location_street = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    location_zip_code = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: false),
                    location_latitude = table.Column<double>(type: "double precision", nullable: false),
                    location_longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operational_bases", x => x.id);
                    table.ForeignKey(
                        name: "fk_operational_bases_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "neighborhoods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    region_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    center_latitude = table.Column<double>(type: "double precision", nullable: false),
                    center_longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_neighborhoods", x => x.id);
                    table.ForeignKey(
                        name: "fk_neighborhoods_regions_region_id",
                        column: x => x.region_id,
                        principalTable: "regions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    brand = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    brand_key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    serial_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    deactivated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deactivated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    search_text = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    location_city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    location_neighborhood_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    location_state = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    location_street = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    location_zip_code = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: true),
                    location_latitude = table.Column<double>(type: "double precision", nullable: true),
                    location_longitude = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment", x => x.id);
                    table.CheckConstraint("ck_equipment_fixed_or_mobile", "(category = 'Fixed' AND location_neighborhood_id IS NOT NULL AND base_id IS NULL) OR (category = 'Mobile' AND location_neighborhood_id IS NULL AND base_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_equipment_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipment_operational_bases_base_id",
                        column: x => x.base_id,
                        principalTable: "operational_bases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipment_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "zip_codes",
                columns: table => new
                {
                    zip_code = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: false),
                    street = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    neighborhood_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_zip_codes", x => x.zip_code);
                    table.ForeignKey(
                        name: "fk_zip_codes_neighborhoods_neighborhood_id",
                        column: x => x.neighborhood_id,
                        principalTable: "neighborhoods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maintenances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    periodicity_days = table.Column<int>(type: "integer", nullable: true),
                    origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    previous_maintenance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    opened_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    result = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    completion_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenances", x => x.id);
                    table.ForeignKey(
                        name: "fk_maintenances_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_maintenances_maintenances_previous_maintenance_id",
                        column: x => x.previous_maintenance_id,
                        principalTable: "maintenances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "equipment_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    origin = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    justification = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    maintenance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_status_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_status_changes_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipment_status_changes_maintenances_maintenance_id",
                        column: x => x.maintenance_id,
                        principalTable: "maintenances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_base_id",
                table: "equipment",
                column: "base_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_equipment_type_id",
                table: "equipment",
                column: "equipment_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_is_active",
                table: "equipment",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_organization_id",
                table: "equipment",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_status",
                table: "equipment",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_brand_key_serial_key",
                table: "equipment",
                columns: new[] { "brand_key", "serial_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_status_changes_equipment_id_occurred_at",
                table: "equipment_status_changes",
                columns: new[] { "equipment_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_status_changes_maintenance_id",
                table: "equipment_status_changes",
                column: "maintenance_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_type_attributes_equipment_type_id_key",
                table: "equipment_type_attributes",
                columns: new[] { "equipment_type_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_types_key",
                table: "equipment_types",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_types_name_key",
                table: "equipment_types",
                column: "name_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_changes_entity_type_entity_id_occurred_at",
                table: "inventory_changes",
                columns: new[] { "entity_type", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenances_equipment_id_state",
                table: "maintenances",
                columns: new[] { "equipment_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_maintenances_previous_maintenance_id",
                table: "maintenances",
                column: "previous_maintenance_id");

            migrationBuilder.CreateIndex(
                name: "ix_maintenances_state_due_date",
                table: "maintenances",
                columns: new[] { "state", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_neighborhoods_name_key",
                table: "neighborhoods",
                column: "name_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_neighborhoods_region_id",
                table: "neighborhoods",
                column: "region_id");

            migrationBuilder.CreateIndex(
                name: "ix_operational_bases_organization_id_name_key",
                table: "operational_bases",
                columns: new[] { "organization_id", "name_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_acronym_key",
                table: "organizations",
                column: "acronym_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_name_key",
                table: "organizations",
                column: "name_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regions_name_key",
                table: "regions",
                column: "name_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_zip_codes_neighborhood_id",
                table: "zip_codes",
                column: "neighborhood_id");

            // Escrito à mão: o EF Core 10 não mapeia índice nem chave estrangeira sobre coluna de tipo complexo, e a
            // localização é tipo complexo (plan 12, 15.13). O modelo não os conhece, então nenhuma migration futura os
            // remove.
            migrationBuilder.CreateIndex(
                name: "ix_equipment_location_neighborhood_id",
                table: "equipment",
                column: "location_neighborhood_id");

            migrationBuilder.AddForeignKey(
                name: "fk_equipment_neighborhoods_location_neighborhood_id",
                table: "equipment",
                column: "location_neighborhood_id",
                principalTable: "neighborhoods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_operational_bases_neighborhoods_location_neighborhood_id",
                table: "operational_bases",
                column: "location_neighborhood_id",
                principalTable: "neighborhoods",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_equipment_neighborhoods_location_neighborhood_id",
                table: "equipment");

            migrationBuilder.DropForeignKey(
                name: "fk_operational_bases_neighborhoods_location_neighborhood_id",
                table: "operational_bases");

            migrationBuilder.DropTable(
                name: "business_parameters");

            migrationBuilder.DropTable(
                name: "equipment_status_changes");

            migrationBuilder.DropTable(
                name: "equipment_type_attributes");

            migrationBuilder.DropTable(
                name: "inventory_changes");

            migrationBuilder.DropTable(
                name: "municipalities");

            migrationBuilder.DropTable(
                name: "zip_codes");

            migrationBuilder.DropTable(
                name: "maintenances");

            migrationBuilder.DropTable(
                name: "neighborhoods");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "regions");

            migrationBuilder.DropTable(
                name: "equipment_types");

            migrationBuilder.DropTable(
                name: "operational_bases");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
