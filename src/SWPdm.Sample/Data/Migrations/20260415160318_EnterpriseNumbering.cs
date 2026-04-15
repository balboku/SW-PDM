using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SWPdm.Sample.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdm_number_sequences",
                columns: table => new
                {
                    sequence_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    prefix = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    current_value = table.Column<int>(type: "integer", nullable: false),
                    last_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_number_sequences", x => x.sequence_id);
                });

            migrationBuilder.CreateTable(
                name: "pdm_numbering_rules",
                columns: table => new
                {
                    rule_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdm_numbering_rules", x => x.rule_id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_pdm_number_sequences_prefix",
                table: "pdm_number_sequences",
                column: "prefix",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_pdm_numbering_rules_document_type",
                table: "pdm_numbering_rules",
                column: "document_type",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdm_number_sequences");

            migrationBuilder.DropTable(
                name: "pdm_numbering_rules");
        }
    }
}
