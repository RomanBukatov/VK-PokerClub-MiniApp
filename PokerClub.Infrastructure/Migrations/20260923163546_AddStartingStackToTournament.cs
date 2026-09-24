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
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("ALTER TABLE \"Tournaments\" ADD COLUMN IF NOT EXISTS \"StartingStack\" integer NOT NULL DEFAULT 10000;");
            }
            else
            {
                migrationBuilder.AddColumn<int>(
                    name: "StartingStack",
                    table: "Tournaments",
                    type: "integer",
                    nullable: false,
                    defaultValue: 10000);
            }
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
