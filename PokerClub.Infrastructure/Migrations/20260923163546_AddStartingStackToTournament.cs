using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerClub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStartingStackToTournament : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StartingStack",
                table: "Tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 10000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartingStack",
                table: "Tournaments");
        }
    }
}
