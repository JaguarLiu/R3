using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R3.Migrations
{
    /// <inheritdoc />
    public partial class AddTripShareAndMemberParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "Trips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShareTokenExpiresAt",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ParticipantId",
                table: "TripMembers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ShareToken",
                table: "Trips",
                column: "ShareToken",
                unique: true,
                filter: "\"ShareToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TripMembers_ParticipantId",
                table: "TripMembers",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_TripMembers_TripId_ParticipantId",
                table: "TripMembers",
                columns: new[] { "TripId", "ParticipantId" },
                unique: true,
                filter: "\"ParticipantId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_TripMembers_Participants_ParticipantId",
                table: "TripMembers",
                column: "ParticipantId",
                principalTable: "Participants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripMembers_Participants_ParticipantId",
                table: "TripMembers");

            migrationBuilder.DropIndex(
                name: "IX_Trips_ShareToken",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_TripMembers_ParticipantId",
                table: "TripMembers");

            migrationBuilder.DropIndex(
                name: "IX_TripMembers_TripId_ParticipantId",
                table: "TripMembers");

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ShareTokenExpiresAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ParticipantId",
                table: "TripMembers");
        }
    }
}
