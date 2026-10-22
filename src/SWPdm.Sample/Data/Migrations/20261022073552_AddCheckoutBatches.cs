using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SWPdm.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdm_checkout_batches",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    root_document_id = table.Column<long>(type: "bigint", nullable: false),
                    actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_checkout_batches", x => x.batch_id);
                });

            migrationBuilder.CreateTable(
                name: "pdm_checkout_batch_items",
                columns: table => new
                {
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    lock_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    restored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_checkout_batch_items", x => new { x.batch_id, x.document_id });
                    table.ForeignKey(
                        name: "FK_pdm_checkout_batch_items_pdm_checkout_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "pdm_checkout_batches",
                        principalColumn: "batch_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_checkout_batches_root_created",
                table: "pdm_checkout_batches",
                columns: new[] { "root_document_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdm_checkout_batch_items");

            migrationBuilder.DropTable(
                name: "pdm_checkout_batches");
        }
    }
}
