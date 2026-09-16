using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerClub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGamificationAndSheetsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedTermsAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AvgPlace",
                table: "Users",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ClubCardId",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KnockoutsCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Top10Count",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Top3Count",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TournamentsPlayed",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WinsCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedTermsAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvgPlace",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ClubCardId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "KnockoutsCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Top10Count",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Top3Count",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TournamentsPlayed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WinsCount",
                table: "Users");
        }
    }
}
