using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace R3.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LineUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LineGroupId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    RawText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedAt",
                table: "Expenses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_LineUserId",
                table: "Expenses",
                column: "LineUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");
        }
    }
}
