using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SWPdm.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateStorageFileIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_pdm_document_versions_storage_file_id",
                table: "pdm_document_versions");

            migrationBuilder.CreateIndex(
                name: "idx_pdm_document_versions_storage_file_id",
                table: "pdm_document_versions",
                column: "storage_file_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_pdm_document_versions_storage_file_id",
                table: "pdm_document_versions");

            migrationBuilder.CreateIndex(
                name: "uq_pdm_document_versions_storage_file_id",
                table: "pdm_document_versions",
                column: "storage_file_id",
                unique: true);
        }
    }
}
