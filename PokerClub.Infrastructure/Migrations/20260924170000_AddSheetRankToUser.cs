using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerClub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetRankToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"SheetRank\" integer NULL;");
            }
            else
            {
                migrationBuilder.AddColumn<int>(
                    name: "SheetRank",
                    table: "Users",
                    type: "integer",
                    nullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SheetRank",
                table: "Users");
        }
    }
}
