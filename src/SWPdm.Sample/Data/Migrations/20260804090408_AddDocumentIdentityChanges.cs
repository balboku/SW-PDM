using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SWPdm.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIdentityChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdm_document_identity_changes",
                columns: table => new
                {
                    identity_change_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_document_id = table.Column<long>(type: "bigint", nullable: false),
                    source_version_id = table.Column<long>(type: "bigint", nullable: false),
                    target_document_id = table.Column<long>(type: "bigint", nullable: false),
                    old_part_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    new_part_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    change_reason = table.Column<string>(type: "text", nullable: false),
                    changed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_document_identity_changes", x => x.identity_change_id);
                    table.ForeignKey(
                        name: "FK_pdm_document_identity_changes_pdm_document_versions_source_~",
                        column: x => x.source_version_id,
                        principalTable: "pdm_document_versions",
                        principalColumn: "version_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pdm_document_identity_changes_pdm_documents_source_document~",
                        column: x => x.source_document_id,
                        principalTable: "pdm_documents",
                        principalColumn: "document_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pdm_document_identity_changes_pdm_documents_target_document~",
                        column: x => x.target_document_id,
                        principalTable: "pdm_documents",
                        principalColumn: "document_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_pdm_identity_changes_source_document",
                table: "pdm_document_identity_changes",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdm_document_identity_changes_source_version_id",
                table: "pdm_document_identity_changes",
                column: "source_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_pdm_identity_changes_target_document",
                table: "pdm_document_identity_changes",
                column: "target_document_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdm_document_identity_changes");
        }
    }
}
