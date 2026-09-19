using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerClub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationEndToTournament : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationEnd",
                table: "Tournaments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SeasonRating_Desc",
                table: "Users",
                column: "SeasonRating",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TotalRating_Desc",
                table: "Users",
                column: "TotalRating",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_SeasonRating_Desc",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TotalRating_Desc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegistrationEnd",
                table: "Tournaments");
        }
    }
}
