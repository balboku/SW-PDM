using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SWPdm.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeGoogleDriveToLocalStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "google_drive_file_id",
                table: "pdm_document_versions",
                newName: "storage_file_id");

            migrationBuilder.RenameIndex(
                name: "uq_pdm_document_versions_drive_file_id",
                table: "pdm_document_versions",
                newName: "uq_pdm_document_versions_storage_file_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "storage_file_id",
                table: "pdm_document_versions",
                newName: "google_drive_file_id");

            migrationBuilder.RenameIndex(
                name: "uq_pdm_document_versions_storage_file_id",
                table: "pdm_document_versions",
                newName: "uq_pdm_document_versions_drive_file_id");
        }
    }
}
