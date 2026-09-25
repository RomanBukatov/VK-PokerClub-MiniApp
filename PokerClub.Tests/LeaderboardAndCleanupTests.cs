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
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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
        var (seasonLeaderboard, seasonTotal) = await ratingService.GetLeaderboardAsync(limit: 10, offset: 0, type: "season");
        Assert.Equal(2, seasonTotal);
        Assert.Equal(2, seasonLeaderboard.Count);
        Assert.Equal("Лукашенко", seasonLeaderboard[0].LastName);
        Assert.Equal(486, seasonLeaderboard[0].SeasonRating);
        Assert.Equal("Гуляев", seasonLeaderboard[1].LastName);
        Assert.Equal(485, seasonLeaderboard[1].SeasonRating);

        // Общий рейтинг: Гуляев (500) > Лукашенко (449)
        var (allTimeLeaderboard, allTotal) = await ratingService.GetLeaderboardAsync(limit: 10, offset: 0, type: "all");
        Assert.Equal(2, allTotal);
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
        var seasonResult = await controller.GetLeaderboard(limit: 50, offset: 0, type: "season");
        var okSeason = Assert.IsType<OkObjectResult>(seasonResult.Result);
        var seasonResponse = Assert.IsType<LeaderboardResponseDto>(okSeason.Value);
        var seasonList = seasonResponse.Items;

        Assert.Equal(2, seasonResponse.TotalCount);
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
        var allResult = await controller.GetLeaderboard(limit: 50, offset: 0, type: "all");
        var okAll = Assert.IsType<OkObjectResult>(allResult.Result);
        var allResponse = Assert.IsType<LeaderboardResponseDto>(okAll.Value);
        var allList = allResponse.Items;

        Assert.Equal(2, allResponse.TotalCount);
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
    public async Task RatingsController_GetLeaderboard_WithSheetRank_UsesCalculatedForSeasonAndSheetRankForAllTime()
    {
        using var context = CreateInMemoryDbContext();

        var player1 = new User { VkId = "sheet_1", FirstName = "Яна", LastName = "Федотова", SeasonRating = 100, TotalRating = 50, SheetRank = 157 };
        var player2 = new User { VkId = "sheet_2", FirstName = "Иван", LastName = "Петров", SeasonRating = 80, TotalRating = 40, SheetRank = null };

        context.Users.AddRange(player1, player2);
        await context.SaveChangesAsync();

        var ratingService = new RatingService(context);
        var controller = new RatingsController(ratingService);

        // 1. Season leaderboard: rank must be calculated (1, 2) and SheetRank must be null
        var seasonResult = await controller.GetLeaderboard(limit: 50, offset: 0, type: "season");
        var okSeason = Assert.IsType<OkObjectResult>(seasonResult.Result);
        var seasonResponse = Assert.IsType<LeaderboardResponseDto>(okSeason.Value);
        var seasonList = seasonResponse.Items;

        Assert.Equal(2, seasonList.Count);
        Assert.Equal(1, seasonList[0].Rank);
        Assert.Null(seasonList[0].SheetRank);
        Assert.Equal(2, seasonList[1].Rank);
        Assert.Null(seasonList[1].SheetRank);

        // 2. All-time leaderboard:
        // player1 with points 50 and SheetRank 157 should have Rank 157 and SheetRank 157
        // player2 with points 40 and SheetRank null should have calculated rank (2) and SheetRank null
        var allResult = await controller.GetLeaderboard(limit: 50, offset: 0, type: "all");
        var okAll = Assert.IsType<OkObjectResult>(allResult.Result);
        var allResponse = Assert.IsType<LeaderboardResponseDto>(okAll.Value);
        var allList = allResponse.Items;

        var yana = allList.First(u => u.FirstName == "Яна");
        Assert.Equal(157, yana.Rank);
        Assert.Equal(157, yana.SheetRank);

        var ivan = allList.First(u => u.FirstName == "Иван");
        Assert.Equal(2, ivan.Rank);
        Assert.Null(ivan.SheetRank);
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

        var result = await controller.GetLeaderboard(limit: requestLimit, offset: 0, type: type);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LeaderboardResponseDto>(okResult.Value);
        var list = response.Items;

        Assert.Equal(expectedLimit, list.Count);
        Assert.Equal(1, list[0].Rank);
        Assert.Equal("Василий", list[0].FirstName);
        Assert.Equal(120, response.TotalCount);

        if (type == "all")
        {
            Assert.Equal(999, list[0].Points);
        }
        else
        {
            Assert.Equal(999, list[0].Points);
        }
    }

    [Fact]
    public async Task RatingsController_Pagination_ReturnsCorrectOffsetAndCalculatedRanks()
    {
        using var context = CreateInMemoryDbContext();

        var users = new List<User>();
        for (int i = 1; i <= 60; i++)
        {
            users.Add(new User
            {
                VkId = $"user_{i}",
                FirstName = $"Игрок{i}",
                LastName = $"Тест{i}",
                SeasonRating = 1000 - i,
                TotalRating = 1000 - i
            });
        }
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var ratingService = new RatingService(context);
        var controller = new RatingsController(ratingService);

        // Page 1: limit 50, offset 0
        var page1Result = await controller.GetLeaderboard(limit: 50, offset: 0, type: "season");
        var ok1 = Assert.IsType<OkObjectResult>(page1Result.Result);
        var resp1 = Assert.IsType<LeaderboardResponseDto>(ok1.Value);
        Assert.Equal(60, resp1.TotalCount);
        Assert.Equal(50, resp1.Items.Count);
        Assert.Equal(1, resp1.Items[0].Rank);
        Assert.Equal(50, resp1.Items[49].Rank);

        // Page 2: limit 50, offset 50
        var page2Result = await controller.GetLeaderboard(limit: 50, offset: 50, type: "season");
        var ok2 = Assert.IsType<OkObjectResult>(page2Result.Result);
        var resp2 = Assert.IsType<LeaderboardResponseDto>(ok2.Value);
        Assert.Equal(60, resp2.TotalCount);
        Assert.Equal(10, resp2.Items.Count);
        Assert.Equal(51, resp2.Items[0].Rank);
        Assert.Equal(60, resp2.Items[9].Rank);
    }

    [Theory]
    [InlineData("1001", "Алексей", "Крылов", true)]
    [InlineData("1024", "Сергей", "Волков", true)]
    [InlineData("user1", "Любое", "Имя", true)]
    [InlineData("user99", "Тест", "Тестов", true)]
    [InlineData("USER_abc", "Тест", "Тестов", true)]
    [InlineData("123456789", "Станислав", "Костров", false)]
    [InlineData("sheet_123", "Василий", "Лукашенко", false)]
    [InlineData("987654321", "Алексей", "Крылов", false)] // Реальный игрок с именем из старого списка ботов
    [InlineData("tg_555", "Сергей", "Волков", false)]     // Реальный игрок Telegram с именем из старого списка ботов
    [InlineData("1000", "Игрок", "Тест", false)]
    [InlineData("1025", "Игрок", "Тест", false)]
    [InlineData(null, "Безымянный", "Игрок", false)]
    [InlineData("", "", "", false)]
    [InlineData("   ", "Пробелы", "Пробелов", false)]
    public void IsFakeBot_OnlyDetectsExplicitBotPatterns_ProtectsRealPlayers(string? vkId, string first, string last, bool expectedIsBot)
    {
        var user = new User
        {
            VkId = vkId!,
            FirstName = first,
            LastName = last
        };

        var isBot = DbInitializer.IsFakeBot(user);
        Assert.Equal(expectedIsBot, isBot);
    }

    [Fact]
    public async Task AssignPointsAndFinishTournamentAsync_UpdatesBothTotalAndSeasonRating()
    {
        using var context = CreateInMemoryDbContext();

        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", City = city, IsActive = true };
        context.Cities.Add(city);
        context.Clubs.Add(club);

        var tour = new Tournament
        {
            Club = club,
            Title = "Friday Rebuy",
            Format = "NL Holdem",
            BuyIn = 1500,
            MaxSeats = 20,
            StartTime = DateTime.UtcNow.AddHours(-3),
            Status = TournamentStatus.RegistrationOpen
        };
        context.Tournaments.Add(tour);

        var player1 = new User { VkId = "player_1", FirstName = "Денис", LastName = "Заббаров", TotalRating = 0, SeasonRating = 0 };
        var player2 = new User { VkId = "player_2", FirstName = "Игорь", LastName = "Гуляев", TotalRating = 50, SeasonRating = 50 };
        context.Users.AddRange(player1, player2);
        await context.SaveChangesAsync();

        var reg1 = new Registration { TournamentId = tour.Id, UserId = player1.Id, Status = RegStatus.Active };
        var reg2 = new Registration { TournamentId = tour.Id, UserId = player2.Id, Status = RegStatus.Active };
        context.Registrations.AddRange(reg1, reg2);
        await context.SaveChangesAsync();

        var ratingService = new RatingService(context);
        var pointsMap = new Dictionary<int, int>
        {
            { player1.Id, 120 },
            { player2.Id, 80 }
        };

        var (success, message) = await ratingService.AssignPointsAndFinishTournamentAsync(tour.Id, pointsMap);

        Assert.True(success);

        var updatedTour = await context.Tournaments.FindAsync(tour.Id);
        Assert.Equal(TournamentStatus.Finished, updatedTour?.Status);

        var updatedP1 = await context.Users.FindAsync(player1.Id);
        Assert.NotNull(updatedP1);
        Assert.Equal(120, updatedP1.TotalRating);
        Assert.Equal(120, updatedP1.SeasonRating); // SeasonRating equals TotalRating

        var updatedP2 = await context.Users.FindAsync(player2.Id);
        Assert.NotNull(updatedP2);
        Assert.Equal(80, updatedP2.TotalRating);
        Assert.Equal(80, updatedP2.SeasonRating); // SeasonRating equals TotalRating
    }

    [Fact]
    public async Task AssignPointsAndFinishTournamentAsync_InvalidTournament_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var ratingService = new RatingService(context);

        var (success, message) = await ratingService.AssignPointsAndFinishTournamentAsync(99999, new Dictionary<int, int>());

        Assert.False(success);
        Assert.Equal("Турнир не найден.", message);
    }

    [Fact]
    public async Task CleanFakeTournamentsAsync_RemovesSeedTournamentsAndRegistrations_PreservesRealTournaments()
    {
        using var context = CreateInMemoryDbContext();

        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        context.Cities.Add(city);
        await context.SaveChangesAsync();

        var club = new Club { Name = "Monte Carlo", CityId = city.Id, IsActive = true };
        context.Clubs.Add(club);
        await context.SaveChangesAsync();

        var user = new User { VkId = "100", FirstName = "Игрок", TotalRating = 10 };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // 4 тестовых сид-турнира с датами 4-8 сентября 2026
        var fakeTour1 = new Tournament { ClubId = club.Id, Title = "Freeroll Tournament", BuyIn = 0, MaxSeats = 30, StartTime = new DateTime(2026, 9, 4, 19, 0, 0, DateTimeKind.Utc) };
        var fakeTour2 = new Tournament { ClubId = club.Id, Title = "Texas Holdem DeepStack", BuyIn = 1500, MaxSeats = 40, StartTime = new DateTime(2026, 9, 5, 20, 0, 0, DateTimeKind.Utc) };
        var fakeTour3 = new Tournament { ClubId = club.Id, Title = "Sunday Grand Event", BuyIn = 3000, MaxSeats = 30, StartTime = new DateTime(2026, 9, 7, 18, 0, 0, DateTimeKind.Utc) };
        var fakeTour4 = new Tournament { ClubId = club.Id, Title = "Weekly Club Cup", BuyIn = 1000, MaxSeats = 20, StartTime = new DateTime(2026, 9, 8, 19, 0, 0, DateTimeKind.Utc) };

        // Реальный турнир, созданный администратором
        var realTour = new Tournament { ClubId = club.Id, Title = "BLACKOUT BOMB POT", BuyIn = 2500, MaxSeats = 50, StartTime = new DateTime(2026, 9, 20, 19, 0, 0, DateTimeKind.Utc) };

        // Реальный турнир с названием "Freeroll Tournament", созданный администратором на актуальную дату (вне 4-8 сентября)
        var realFreeroll = new Tournament { ClubId = club.Id, Title = "Freeroll Tournament", BuyIn = 0, MaxSeats = 40, StartTime = new DateTime(2026, 9, 25, 19, 0, 0, DateTimeKind.Utc) };

        context.Tournaments.AddRange(fakeTour1, fakeTour2, fakeTour3, fakeTour4, realTour, realFreeroll);
        await context.SaveChangesAsync();

        // Регистрация на фейковый турнир и на реальные турниры
        var regFake = new Registration { TournamentId = fakeTour1.Id, UserId = user.Id, Status = RegStatus.Active };
        var regReal1 = new Registration { TournamentId = realTour.Id, UserId = user.Id, Status = RegStatus.Active };
        var regReal2 = new Registration { TournamentId = realFreeroll.Id, UserId = user.Id, Status = RegStatus.Active };
        context.Registrations.AddRange(regFake, regReal1, regReal2);
        await context.SaveChangesAsync();

        // Выполняем очистку
        await DbInitializer.CleanFakeTournamentsAsync(context);

        var remainingTournaments = await context.Tournaments.OrderBy(t => t.Id).ToListAsync();
        Assert.Equal(2, remainingTournaments.Count);
        Assert.Contains(remainingTournaments, t => t.Title == "BLACKOUT BOMB POT");
        Assert.Contains(remainingTournaments, t => t.Title == "Freeroll Tournament" && t.StartTime == realFreeroll.StartTime);

        var remainingRegs = await context.Registrations.ToListAsync();
        Assert.Equal(2, remainingRegs.Count);
        Assert.Contains(remainingRegs, r => r.TournamentId == realTour.Id);
        Assert.Contains(remainingRegs, r => r.TournamentId == realFreeroll.Id);
    }

    [Fact]
    public async Task SeedAsync_DoesNotCreateFakeTournaments()
    {
        using var context = CreateInMemoryDbContext();

        await DbInitializer.SeedAsync(context);

        // Город, клуб и админ созданы
        Assert.True(await context.Cities.AnyAsync());
        Assert.True(await context.Clubs.AnyAsync());
        Assert.True(await context.Users.AnyAsync(u => u.VkId == "123456789"));

        // Сид-турниров быть НЕ должно!
        var tournaments = await context.Tournaments.ToListAsync();
        Assert.Empty(tournaments);
    }

    [Fact]
    public async Task CleanFakeUsersAsync_ResetsCorruptedClubCardIdMatchingSheetRank()
    {
        using var context = CreateInMemoryDbContext();

        // Пользователь с ошибочно записанным ClubCardId = "5", равным SheetRank = 5
        var userWithCorruptedCard = new User
        {
            VkId = "sheet_5_igor",
            FirstName = "Игорь",
            LastName = "Гуляев",
            TotalRating = 504,
            SheetRank = 5,
            ClubCardId = "5"
        };

        // Пользователь с ошибочно записанным ClubCardId = "22", равным TournamentsPlayed = 22
        var userWithTournamentsCard = new User
        {
            VkId = "sheet_7_tournaments",
            FirstName = "Сергей",
            LastName = "Иванов",
            TotalRating = 100,
            SheetRank = 7,
            TournamentsPlayed = 22,
            ClubCardId = "22"
        };

        // Пользователь с настоящей картой, не совпадающей с рангом
        var userWithValidCard = new User
        {
            VkId = "sheet_1_vasya",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 486,
            SheetRank = 1,
            TournamentsPlayed = 22,
            ClubCardId = "1060"
        };

        context.Users.AddRange(userWithCorruptedCard, userWithTournamentsCard, userWithValidCard);
        await context.SaveChangesAsync();

        await DbInitializer.CleanFakeUsersAsync(context);

        var updatedIgor = await context.Users.FirstAsync(u => u.VkId == "sheet_5_igor");
        Assert.Null(updatedIgor.ClubCardId); // Сброшено в null

        var updatedSergey = await context.Users.FirstAsync(u => u.VkId == "sheet_7_tournaments");
        Assert.Null(updatedSergey.ClubCardId); // Сброшено в null

        var updatedVasya = await context.Users.FirstAsync(u => u.VkId == "sheet_1_vasya");
        Assert.Equal("1060", updatedVasya.ClubCardId); // Сохранено
    }
}
