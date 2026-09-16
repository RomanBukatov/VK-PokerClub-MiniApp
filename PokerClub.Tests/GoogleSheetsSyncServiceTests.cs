using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PokerClub.Domain.Entities;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;
using Xunit;

namespace PokerClub.Tests;

public class GoogleSheetsSyncServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private const string SampleRatingCsv = 
        "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
        "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"486\",\"7,36\"\r\n" +
        "\"2\",\"Гуляев Игорь\",\"26\",\"1\",\"6\",\"10\",\"28\",\"485\",\"4,92\"\r\n" +
        "\"3\",\"Петров Денис Сергеевич\",\"24\",\"0\",\"2\",\"10\",\"8\",\"292\",\"5,40\"\r\n";

    private const string SampleRegistrationsCsv =
        "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"Комментарий\"\r\n" +
        "\"15 сентября | 19:00 FREEROLL\",\"Гуляев Игорь\",\"1345\",\"89223636110\",\"\"\r\n" +
        "\"16 сентября | 19:00 FREEROLL\",\"Лукашенко Василий\",\"1060\",\"89027909924\",\"\"\r\n";

    [Fact]
    public void ParseCsv_CorrectlyParsesQuotesCommasAndLines()
    {
        var rows = GoogleSheetsSyncService.ParseCsv(SampleRatingCsv);
        Assert.Equal(4, rows.Count);

        // Header
        Assert.Equal("ОБЩИЙ РЕЙТИНГ КЛУБА Место", rows[0][0]);
        Assert.Equal("Игрок", rows[0][1]);

        // Row 1
        Assert.Equal("1", rows[1][0]);
        Assert.Equal("Лукашенко Василий", rows[1][1]);
        Assert.Equal("22", rows[1][2]);
        Assert.Equal("2", rows[1][3]);
        Assert.Equal("5", rows[1][4]);
        Assert.Equal("10", rows[1][5]);
        Assert.Equal("40", rows[1][6]);
        Assert.Equal("486", rows[1][7]);
        Assert.Equal("7,36", rows[1][8]);
    }

    [Fact]
    public async Task SyncFromCsv_WhenUsersDoNotExist_CreatesNewUsersWithSheetStats()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromCsvAsync(SampleRatingCsv);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalProcessed);
        Assert.Equal(3, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);

        var users = await context.Users.ToListAsync();
        Assert.Equal(3, users.Count);

        var lukashenko = users.FirstOrDefault(u => u.LastName == "Лукашенко" && u.FirstName == "Василий");
        Assert.NotNull(lukashenko);
        Assert.Equal(486, lukashenko.TotalRating);
        Assert.Equal(22, lukashenko.TournamentsPlayed);
        Assert.Equal(2, lukashenko.WinsCount);
        Assert.Equal(5, lukashenko.Top3Count);
        Assert.Equal(10, lukashenko.Top10Count);
        Assert.Equal(40, lukashenko.KnockoutsCount);
        Assert.Equal(7.36, lukashenko.AvgPlace);
        // Leaderboard place should NOT be mistakenly set as ClubCardId
        Assert.Null(lukashenko.ClubCardId);
        Assert.StartsWith("sheet_1_", lukashenko.VkId);
    }

    [Fact]
    public async Task SyncFromCsv_WithRegistrationsSheet_CorrectlyAssignsRealClubCardIdAndPhone()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromCsvAsync(SampleRatingCsv, SampleRegistrationsCsv);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalProcessed);
        Assert.Equal(3, result.CreatedCount);

        var users = await context.Users.ToListAsync();
        var gulyaev = users.FirstOrDefault(u => u.LastName == "Гуляев" && u.FirstName == "Игорь");
        Assert.NotNull(gulyaev);
        Assert.Equal("1345", gulyaev.ClubCardId);
        Assert.Equal("89223636110", gulyaev.PhoneNumber);
        Assert.Equal(485, gulyaev.TotalRating);

        var lukashenko = users.FirstOrDefault(u => u.LastName == "Лукашенко" && u.FirstName == "Василий");
        Assert.NotNull(lukashenko);
        Assert.Equal("1060", lukashenko.ClubCardId);
        Assert.Equal("89027909924", lukashenko.PhoneNumber);
    }

    [Fact]
    public async Task SyncFromCsv_WhenUserExistsByNameReversed_MatchesAndUpdatesUser()
    {
        using var context = CreateInMemoryDbContext();
        var existingUser = new User
        {
            VkId = "vk_real_123",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 10,
            TournamentsPlayed = 1
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromCsvAsync(SampleRatingCsv);

        Assert.True(result.Success);
        Assert.Equal(3, result.TotalProcessed);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(2, result.CreatedCount);

        var updatedUser = await context.Users.FirstAsync(u => u.VkId == "vk_real_123");
        Assert.Equal(486, updatedUser.TotalRating);
        Assert.Equal(22, updatedUser.TournamentsPlayed);
        Assert.Equal(2, updatedUser.WinsCount);
        Assert.Equal(5, updatedUser.Top3Count);
        Assert.Equal(10, updatedUser.Top10Count);
        Assert.Equal(40, updatedUser.KnockoutsCount);
        Assert.Equal(7.36, updatedUser.AvgPlace);
    }

    [Fact]
    public async Task SyncFromCsv_WhenUserExistsByClubCardId_MatchesAndUpdatesUser()
    {
        using var context = CreateInMemoryDbContext();
        var existingUser = new User
        {
            VkId = "vk_custom_456",
            FirstName = "Игорь",
            LastName = "Незнакомов",
            ClubCardId = "1345", // Matched with Gulyaev Igor via registrations sheet
            TotalRating = 0
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromCsvAsync(SampleRatingCsv, SampleRegistrationsCsv);

        Assert.True(result.Success);
        Assert.Equal(1, result.UpdatedCount);

        var updated = await context.Users.FirstAsync(u => u.VkId == "vk_custom_456");
        Assert.Equal(485, updated.TotalRating);
        Assert.Equal(26, updated.TournamentsPlayed);
        Assert.Equal(1, updated.WinsCount);
        Assert.Equal(6, updated.Top3Count);
        Assert.Equal(4.92, updated.AvgPlace);
    }

    [Fact]
    public void TokensMatch_WhenOnlyOneGenericTokenMatches_ReturnsFalse()
    {
        // "Александр" should not falsely match "Оборин Александр"
        var t1 = GoogleSheetsSyncService.TokenizeName("Александр");
        var t2 = GoogleSheetsSyncService.TokenizeName("Оборин Александр");

        Assert.False(GoogleSheetsSyncService.TokensMatch(t1, t2));
    }

    [Fact]
    public void TokensMatch_WhenTwoTokensMatch_ReturnsTrue()
    {
        var t1 = GoogleSheetsSyncService.TokenizeName("Лукашенко Василий");
        var t2 = GoogleSheetsSyncService.TokenizeName("Василий Лукашенко");

        Assert.True(GoogleSheetsSyncService.TokensMatch(t1, t2));

        var t3 = GoogleSheetsSyncService.TokenizeName("Петров Денис Сергеевич");
        var t4 = GoogleSheetsSyncService.TokenizeName("Денис Петров");

        Assert.True(GoogleSheetsSyncService.TokensMatch(t3, t4));
    }

    [Fact]
    public async Task SyncFromCsv_WithFormattedNumbersHavingSpaces_ParsesCorrectly()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var csvWithSpaces = 
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Иванов Иван\",\" 10 \",\" 2 \",\" 3 \",\" 5 \",\" 12 \",\" 1 250 \",\" 3,50 \"\r\n";

        var result = await service.SyncFromCsvAsync(csvWithSpaces);
        Assert.True(result.Success);
        Assert.Equal(1, result.CreatedCount);

        var user = await context.Users.FirstAsync();
        Assert.Equal(1250, user.TotalRating);
        Assert.Equal(10, user.TournamentsPlayed);
        Assert.Equal(3.5, user.AvgPlace);
    }

    [Fact]
    public async Task SyncFromCsv_WithEmptyCsv_ReturnsError()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromCsvAsync("");

        Assert.False(result.Success);
        Assert.Equal(0, result.TotalProcessed);
    }

    [Fact]
    public async Task SyncFromCsv_WithBothSeasonAndTotalSheets_PopulatesBothRatingsAndStats()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var sampleSeasonCsv = 
            "\"РЕЙТИНГ СЕЗОНА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"486\",\"7,36\"\r\n" +
            "\"2\",\"Гуляев Игорь\",\"26\",\"1\",\"6\",\"10\",\"28\",\"485\",\"4,92\"\r\n";

        var sampleTotalCsv = 
            "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"449\",\"7,36\"\r\n" +
            "\"2\",\"Гуляев Игорь\",\"26\",\"1\",\"6\",\"10\",\"28\",\"440\",\"4,92\"\r\n";

        var result = await service.SyncFromCsvAsync(sampleSeasonCsv, sampleTotalCsv, SampleRegistrationsCsv);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(2, result.CreatedCount);

        var lukashenko = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Лукашенко" && u.FirstName == "Василий");
        Assert.NotNull(lukashenko);
        Assert.Equal(486, lukashenko.SeasonRating);
        Assert.Equal(449, lukashenko.TotalRating);
        Assert.Equal(22, lukashenko.TournamentsPlayed);
        Assert.Equal(2, lukashenko.WinsCount);
        Assert.Equal(5, lukashenko.Top3Count);
        Assert.Equal(10, lukashenko.Top10Count);
        Assert.Equal(40, lukashenko.KnockoutsCount);
        Assert.Equal(7.36, lukashenko.AvgPlace);
        Assert.Equal("1060", lukashenko.ClubCardId);
        Assert.Equal("89027909924", lukashenko.PhoneNumber);

        var gulyaev = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Гуляев" && u.FirstName == "Игорь");
        Assert.NotNull(gulyaev);
        Assert.Equal(485, gulyaev.SeasonRating);
        Assert.Equal(440, gulyaev.TotalRating);
        Assert.Equal("1345", gulyaev.ClubCardId);
        Assert.Equal("89223636110", gulyaev.PhoneNumber);
    }
}
