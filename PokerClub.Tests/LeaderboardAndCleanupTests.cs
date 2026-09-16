using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PokerClub.Api.Controllers;
using PokerClub.Api.DTOs;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;
using Xunit;

namespace PokerClub.Tests;

public class LeaderboardAndCleanupTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CleanFakeUsersAsync_RemovesFakeBotsAndResetsAdminRating()
    {
        using var context = CreateInMemoryDbContext();

        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        context.Cities.Add(city);
        await context.SaveChangesAsync();

        var club = new Club { Name = "Monte Carlo", CityId = city.Id, IsActive = true };
        context.Clubs.Add(club);
        await context.SaveChangesAsync();

        var tournament = new Tournament
        {
            ClubId = club.Id,
            Title = "Weekly Cup",
            BuyIn = 1000,
            MaxSeats = 20,
            StartTime = DateTime.UtcNow.AddDays(-1),
            Status = TournamentStatus.Finished
        };
        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync();

        // Тестовые боты
        var bot1 = new User { VkId = "1001", FirstName = "Алексей", LastName = "Крылов", TotalRating = 2840, SeasonRating = 2840 };
        var bot2 = new User { VkId = "1002", FirstName = "Сергей", LastName = "Волков", TotalRating = 2410, SeasonRating = 2410 };
        var bot3 = new User { VkId = "user3", FirstName = "Дмитрий", LastName = "Морозов", TotalRating = 1950, SeasonRating = 1950 };
        
        // Тестовый админ
        var admin = new User { VkId = "123456789", FirstName = "Станислав", LastName = "Костров", TotalRating = 2450, SeasonRating = 2450 };

        // Реальный игрок из Google Sheets
        var realUser = new User { VkId = "sheet_1_lukashenko", FirstName = "Василий", LastName = "Лукашенко", TotalRating = 449, SeasonRating = 486 };

        // Реальный пользователь VK, вошедший через Mini App
        var realVkUser = new User { VkId = "987654321", FirstName = "Иван", LastName = "Петров", TotalRating = 120, SeasonRating = 150 };

        context.Users.AddRange(bot1, bot2, bot3, admin, realUser, realVkUser);
        await context.SaveChangesAsync();

        // Регистрация бота на турнир
        var regBot = new Registration { TournamentId = tournament.Id, UserId = bot1.Id, Status = RegStatus.Played, PointsEarned = 100 };
        var regReal = new Registration { TournamentId = tournament.Id, UserId = realUser.Id, Status = RegStatus.Played, PointsEarned = 50 };
        var regVk = new Registration { TournamentId = tournament.Id, UserId = realVkUser.Id, Status = RegStatus.Active };
        context.Registrations.AddRange(regBot, regReal, regVk);
        await context.SaveChangesAsync();

        // Выполняем очистку
        await DbInitializer.CleanFakeUsersAsync(context);

        var remainingUsers = await context.Users.ToListAsync();
        Assert.Equal(3, remainingUsers.Count);

        var lukashenko = remainingUsers.FirstOrDefault(u => u.VkId == "sheet_1_lukashenko");
        Assert.NotNull(lukashenko);
        Assert.Equal(486, lukashenko.SeasonRating);
        Assert.Equal(449, lukashenko.TotalRating);

        var testAdmin = remainingUsers.FirstOrDefault(u => u.VkId == "123456789");
        Assert.NotNull(testAdmin);
        Assert.Equal(0, testAdmin.TotalRating);
        Assert.Equal(0, testAdmin.SeasonRating);

        // Реальный пользователь VK не удален!
        var preservedVkUser = remainingUsers.FirstOrDefault(u => u.VkId == "987654321");
        Assert.NotNull(preservedVkUser);
        Assert.Equal(120, preservedVkUser.TotalRating);
        Assert.Equal(150, preservedVkUser.SeasonRating);

        // Регистрация бота удалена, регистрации реальных пользователей остались
        var remainingRegs = await context.Registrations.ToListAsync();
        Assert.Equal(2, remainingRegs.Count);
        Assert.Contains(remainingRegs, r => r.UserId == realUser.Id);
        Assert.Contains(remainingRegs, r => r.UserId == realVkUser.Id);
    }

    [Fact]
    public async Task RatingService_GetLeaderboardAsync_CorrectlyFiltersAndOrdersByType()
    {
        using var context = CreateInMemoryDbContext();

        var lukashenko = new User { VkId = "sheet_1", FirstName = "Василий", LastName = "Лукашенко", SeasonRating = 486, TotalRating = 449 };
        var gulyaev = new User { VkId = "sheet_2", FirstName = "Игорь", LastName = "Гуляев", SeasonRating = 485, TotalRating = 500 };
        var newbie = new User { VkId = "sheet_3", FirstName = "Иван", LastName = "Новичков", SeasonRating = 0, TotalRating = 0 };

        context.Users.AddRange(lukashenko, gulyaev, newbie);
        await context.SaveChangesAsync();

        var ratingService = new RatingService(context);

        // Сезонный рейтинг: Лукашенко (486) > Гуляев (485)
        var seasonLeaderboard = await ratingService.GetLeaderboardAsync(limit: 10, type: "season");
        Assert.Equal(2, seasonLeaderboard.Count);
        Assert.Equal("Лукашенко", seasonLeaderboard[0].LastName);
        Assert.Equal(486, seasonLeaderboard[0].SeasonRating);
        Assert.Equal("Гуляев", seasonLeaderboard[1].LastName);
        Assert.Equal(485, seasonLeaderboard[1].SeasonRating);

        // Общий рейтинг: Гуляев (500) > Лукашенко (449)
        var allTimeLeaderboard = await ratingService.GetLeaderboardAsync(limit: 10, type: "all");
        Assert.Equal(2, allTimeLeaderboard.Count);
        Assert.Equal("Гуляев", allTimeLeaderboard[0].LastName);
        Assert.Equal(500, allTimeLeaderboard[0].TotalRating);
        Assert.Equal("Лукашенко", allTimeLeaderboard[1].LastName);
        Assert.Equal(449, allTimeLeaderboard[1].TotalRating);
    }

    [Fact]
    public async Task RatingsController_GetLeaderboard_ReturnsDtoWithCorrectRanksAndActivePoints()
    {
        using var context = CreateInMemoryDbContext();

        var lukashenko = new User { VkId = "sheet_1", FirstName = "Василий", LastName = "Лукашенко", SeasonRating = 486, TotalRating = 449 };
        var gulyaev = new User { VkId = "sheet_2", FirstName = "Игорь", LastName = "Гуляев", SeasonRating = 485, TotalRating = 440 };

        context.Users.AddRange(lukashenko, gulyaev);
        await context.SaveChangesAsync();

        var ratingService = new RatingService(context);
        var controller = new RatingsController(ratingService);

        // Season leaderboard
        var seasonResult = await controller.GetLeaderboard(limit: 50, type: "season");
        var okSeason = Assert.IsType<OkObjectResult>(seasonResult.Result);
        var seasonList = Assert.IsType<List<LeaderboardEntryDto>>(okSeason.Value);

        Assert.Equal(2, seasonList.Count);
        Assert.Equal(1, seasonList[0].Rank);
        Assert.Equal("Василий", seasonList[0].FirstName);
        Assert.Equal(486, seasonList[0].TotalRating); // Active points
        Assert.Equal(486, seasonList[0].SeasonRating);
        Assert.Equal(486, seasonList[0].Points);

        Assert.Equal(2, seasonList[1].Rank);
        Assert.Equal(485, seasonList[1].TotalRating);
        Assert.Equal(485, seasonList[1].Points);

        // All-time leaderboard
        var allResult = await controller.GetLeaderboard(limit: 50, type: "all");
        var okAll = Assert.IsType<OkObjectResult>(allResult.Result);
        var allList = Assert.IsType<List<LeaderboardEntryDto>>(okAll.Value);

        Assert.Equal(2, allList.Count);
        Assert.Equal(1, allList[0].Rank);
        Assert.Equal("Василий", allList[0].FirstName);
        Assert.Equal(449, allList[0].TotalRating); // Active points for all-time
        Assert.Equal(486, allList[0].SeasonRating);
        Assert.Equal(449, allList[0].Points);

        Assert.Equal(2, allList[1].Rank);
        Assert.Equal(440, allList[1].TotalRating);
        Assert.Equal(440, allList[1].Points);
    }

    [Fact]
    public async Task CleanFakeUsersAsync_IsIdempotent_And_NeverDeletesLinkedRealVkUsers()
    {
        using var context = CreateInMemoryDbContext();

        // 1. Создаем реального VK пользователя
        var realVkUser = new User
        {
            VkId = "555666777",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 449,
            SeasonRating = 486,
            TournamentsPlayed = 22,
            WinsCount = 2,
            Top3Count = 5,
            ClubCardId = "1060"
        };

        // 2. Тестовый бот
        var bot = new User { VkId = "user7", FirstName = "Елена", LastName = "Кузнецова", TotalRating = 120, SeasonRating = 120 };

        // 3. Тестовый админ
        var admin = new User { VkId = "123456789", FirstName = "Станислав", LastName = "Костров", TotalRating = 1000, SeasonRating = 1000 };

        context.Users.AddRange(realVkUser, bot, admin);
        await context.SaveChangesAsync();

        // Запуск очистки 3 раза подряд для проверки идемпотентности
        for (int i = 0; i < 3; i++)
        {
            await DbInitializer.CleanFakeUsersAsync(context);
        }

        var users = await context.Users.ToListAsync();
        Assert.Equal(2, users.Count);

        var lukashenko = users.FirstOrDefault(u => u.VkId == "555666777");
        Assert.NotNull(lukashenko);
        Assert.Equal(486, lukashenko.SeasonRating);
        Assert.Equal(449, lukashenko.TotalRating);
        Assert.Equal("1060", lukashenko.ClubCardId);

        var testAdmin = users.FirstOrDefault(u => u.VkId == "123456789");
        Assert.NotNull(testAdmin);
        Assert.Equal(0, testAdmin.TotalRating);
        Assert.Equal(0, testAdmin.SeasonRating);
    }

    [Theory]
    [InlineData(-10, "season", 50)]
    [InlineData(0, "season", 50)]
    [InlineData(200, "all", 100)]
    [InlineData(50, "invalid_type", 50)]
    public async Task RatingsController_EdgeCases_NormalizesParameters(int requestLimit, string type, int expectedLimit)
    {
        using var context = CreateInMemoryDbContext();

        var users = new List<User>();
        for (int i = 1; i <= 120; i++)
        {
            users.Add(new User
            {
                VkId = $"sheet_{i}",
                FirstName = i == 1 ? "Василий" : $"Игрок{i}",
                LastName = i == 1 ? "Лукашенко" : $"Тест{i}",
                SeasonRating = 1000 - i,
                TotalRating = 1000 - i
            });
        }
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var ratingService = new RatingService(context);
        var controller = new RatingsController(ratingService);

        var result = await controller.GetLeaderboard(limit: requestLimit, type: type);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<LeaderboardEntryDto>>(okResult.Value);

        Assert.Equal(expectedLimit, list.Count);
        Assert.Equal(1, list[0].Rank);
        Assert.Equal("Василий", list[0].FirstName);

        if (type == "all")
        {
            Assert.Equal(999, list[0].Points);
        }
        else
        {
            Assert.Equal(999, list[0].Points);
        }
    }
}
