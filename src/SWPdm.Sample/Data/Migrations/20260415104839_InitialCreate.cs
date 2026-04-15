using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SWPdm.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdm_bom_occurrences",
                columns: table => new
                {
                    bom_occurrence_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    parent_version_id = table.Column<long>(type: "bigint", nullable: false),
                    child_version_id = table.Column<long>(type: "bigint", nullable: true),
                    occurrence_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    parent_configuration_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    child_configuration_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    find_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_reference_path = table.Column<string>(type: "text", nullable: false),
                    package_relative_path = table.Column<string>(type: "text", nullable: false),
                    reference_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_suppressed = table.Column<bool>(type: "boolean", nullable: false),
                    is_virtual = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_bom_occurrences", x => x.bom_occurrence_id);
                    table.CheckConstraint("ck_pdm_bom_occurrences_status", "reference_status IN ('Resolved', 'Broken', 'Virtual', 'Missing')");
                });

            migrationBuilder.CreateTable(
                name: "pdm_custom_properties",
                columns: table => new
                {
                    custom_property_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    configuration_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    property_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    property_value = table.Column<string>(type: "text", nullable: true),
                    property_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raw_expression = table.Column<string>(type: "text", nullable: true),
                    is_resolved = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_custom_properties", x => x.custom_property_id);
                });

            migrationBuilder.CreateTable(
                name: "pdm_document_versions",
                columns: table => new
                {
                    version_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    revision_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    google_drive_file_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_file_path = table.Column<string>(type: "text", nullable: false),
                    vault_relative_path = table.Column<string>(type: "text", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    source_last_write_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    parsed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_document_versions", x => x.version_id);
                });

            migrationBuilder.CreateTable(
                name: "pdm_documents",
                columns: table => new
                {
                    document_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    part_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    revision_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    material = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    designer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    current_version_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_documents", x => x.document_id);
                    table.CheckConstraint("ck_pdm_documents_extension", "file_extension IN ('.sldprt', '.sldasm', '.slddrw')");
                    table.CheckConstraint("ck_pdm_documents_type", "document_type IN ('Part', 'Assembly', 'Drawing')");
                    table.ForeignKey(
                        name: "FK_pdm_documents_pdm_document_versions_current_version_id",
                        column: x => x.current_version_id,
                        principalTable: "pdm_document_versions",
                        principalColumn: "version_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_pdm_bom_occurrences_child",
                table: "pdm_bom_occurrences",
                column: "child_version_id");

            migrationBuilder.CreateIndex(
                name: "idx_pdm_bom_occurrences_parent",
                table: "pdm_bom_occurrences",
                column: "parent_version_id");

            migrationBuilder.CreateIndex(
                name: "idx_pdm_bom_occurrences_status",
                table: "pdm_bom_occurrences",
                columns: new[] { "reference_status", "is_suppressed" });

            migrationBuilder.CreateIndex(
                name: "uq_pdm_bom_occurrences_parent_path",
                table: "pdm_bom_occurrences",
                columns: new[] { "parent_version_id", "occurrence_path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pdm_custom_properties_lookup",
                table: "pdm_custom_properties",
                columns: new[] { "property_name", "property_value" });

            migrationBuilder.CreateIndex(
                name: "uq_pdm_custom_properties",
                table: "pdm_custom_properties",
                columns: new[] { "version_id", "configuration_name", "property_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_pdm_document_versions_document_id",
                table: "pdm_document_versions",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "uq_pdm_document_versions_doc_ver",
                table: "pdm_document_versions",
                columns: new[] { "document_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_pdm_document_versions_drive_file_id",
                table: "pdm_document_versions",
                column: "google_drive_file_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdm_documents_current_version_id",
                table: "pdm_documents",
                column: "current_version_id");

            migrationBuilder.AddForeignKey(
                name: "FK_pdm_bom_occurrences_pdm_document_versions_child_version_id",
                table: "pdm_bom_occurrences",
                column: "child_version_id",
                principalTable: "pdm_document_versions",
                principalColumn: "version_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_pdm_bom_occurrences_pdm_document_versions_parent_version_id",
                table: "pdm_bom_occurrences",
                column: "parent_version_id",
                principalTable: "pdm_document_versions",
                principalColumn: "version_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pdm_custom_properties_pdm_document_versions_version_id",
                table: "pdm_custom_properties",
                column: "version_id",
                principalTable: "pdm_document_versions",
                principalColumn: "version_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_pdm_document_versions_pdm_documents_document_id",
                table: "pdm_document_versions",
                column: "document_id",
                principalTable: "pdm_documents",
                principalColumn: "document_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pdm_documents_pdm_document_versions_current_version_id",
                table: "pdm_documents");

            migrationBuilder.DropTable(
                name: "pdm_bom_occurrences");

            migrationBuilder.DropTable(
                name: "pdm_custom_properties");

            migrationBuilder.DropTable(
                name: "pdm_document_versions");

            migrationBuilder.DropTable(
                name: "pdm_documents");
        }
    }
}
