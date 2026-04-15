using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SWPdm.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "checked_out_at",
                table: "pdm_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "checked_out_by",
                table: "pdm_documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_state",
                table: "pdm_document_versions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "WIP");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pdm_document_versions_lifecycle",
                table: "pdm_document_versions",
                sql: "lifecycle_state IN ('WIP', 'InReview', 'Released', 'Obsolete')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pdm_document_versions_lifecycle",
                table: "pdm_document_versions");

            migrationBuilder.DropColumn(
                name: "checked_out_at",
                table: "pdm_documents");

            migrationBuilder.DropColumn(
                name: "checked_out_by",
                table: "pdm_documents");

            migrationBuilder.DropColumn(
                name: "lifecycle_state",
                table: "pdm_document_versions");
        }
    }
}
