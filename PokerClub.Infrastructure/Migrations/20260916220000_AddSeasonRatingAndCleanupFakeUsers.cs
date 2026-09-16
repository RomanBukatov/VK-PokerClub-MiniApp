using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PokerClub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasonRatingAndCleanupFakeUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeasonRating",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Очистка тестовых ботов и их записей на турниры
            migrationBuilder.Sql("DELETE FROM \"Registrations\" WHERE \"UserId\" IN (SELECT \"Id\" FROM \"Users\" WHERE \"VkId\" NOT LIKE 'sheet_%' AND \"VkId\" != '123456789');");
            migrationBuilder.Sql("DELETE FROM \"Users\" WHERE \"VkId\" NOT LIKE 'sheet_%' AND \"VkId\" != '123456789';");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"TotalRating\" = 0, \"SeasonRating\" = 0 WHERE \"VkId\" = '123456789';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeasonRating",
                table: "Users");
        }
    }
}
