using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R3.Migrations
{
    /// <inheritdoc />
    public partial class AddLineGroupBindingToTrip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LineGroupId",
                table: "Trips",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_LineGroupId",
                table: "Trips",
                column: "LineGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_LineGroupId_IsActive",
                table: "Trips",
                columns: new[] { "LineGroupId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_LineGroupId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_LineGroupId_IsActive",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LineGroupId",
                table: "Trips");
        }
    }
}
