using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PokerClub.Api.Controllers;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Interfaces;
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

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public List<HttpRequestMessage> Requests { get; } = new();

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_EncodesSheetNameInGvizUrl()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("dummy,csv\r\n1,2")
            };
        });
        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Рейтинг сезона", "646289371");

        Assert.Equal("dummy,csv\r\n1,2", result);
        Assert.Single(handler.Requests);
        var rawUri = handler.Requests[0].RequestUri!.OriginalString;
        Assert.Contains("sheet=%D0%A0%D0%B5%D0%B9%D1%82%D0%B8%D0%BD%D0%B3%20%D1%81%D0%B5%D0%B7%D0%BE%D0%BD%D0%B0", rawUri);
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenGvizReturns401_FallsBackToGidExport()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("gviz/tq"))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Unauthorized")
                };
            }

            if (req.RequestUri!.ToString().Contains("export?format=csv&gid=646289371"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleRatingCsv)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Рейтинг сезона", "646289371");

        Assert.Equal(SampleRatingCsv, result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("gviz/tq", handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("export?format=csv&gid=646289371", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenGvizReturnsHtml_FallsBackToGidExport()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("gviz/tq"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<!DOCTYPE html><html><head><title>Sign in</title></head></html>")
                };
            }

            if (req.RequestUri!.ToString().Contains("export?format=csv&gid=0"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleRatingCsv)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Общий рейтинг", "0");

        Assert.Equal(SampleRatingCsv, result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("export?format=csv&gid=0", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenBothFail_ReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Unauthorized")
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Рейтинг сезона", "646289371");

        Assert.Null(result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_SetsUserAgentHeader()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("col1\r\nval1")
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        await service.DownloadCsvWithFallbackAsync("Общий рейтинг", "0");

        Assert.Single(handler.Requests);
        var userAgent = handler.Requests[0].Headers.UserAgent.ToString();
        Assert.Contains("Mozilla/5.0", userAgent);
    }

    [Fact]
    public async Task SyncFromGoogleSheetsAsync_WhenBothSheetsFail_ReturnsErrorMessage()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Unauthorized")
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromGoogleSheetsAsync();

        Assert.False(result.Success);
        Assert.Equal("Не удалось загрузить ни лист «Рейтинг сезона», ни «Общий рейтинг».", result.Message);
    }

    [Fact]
    public async Task SyncFromGoogleSheetsAsync_WhenGvizFailsButGidFallbackSucceeds_SuccessfullySyncs()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            // Gviz fails with 401
            if (uri.Contains("gviz/tq"))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Unauthorized")
                };
            }

            // Gid fallback for season rating succeeds
            if (uri.Contains("export?format=csv&gid=646289371"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "\"РЕЙТИНГ СЕЗОНА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
                        "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"486\",\"7,36\"\r\n")
                };
            }

            // Gid fallback for total rating succeeds
            if (uri.Contains("export?format=csv&gid=0"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
                        "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"449\",\"7,36\"\r\n")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromGoogleSheetsAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(1, result.CreatedCount);

        var user = await context.Users.FirstOrDefaultAsync();
        Assert.NotNull(user);
        Assert.Equal("Лукашенко", user.LastName);
        Assert.Equal(486, user.SeasonRating);
        Assert.Equal(449, user.TotalRating);
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenGvizThrowsException_FallsBackToGidExport()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("gviz/tq"))
            {
                throw new HttpRequestException("Network failure on gviz");
            }

            if (req.RequestUri!.ToString().Contains("export?format=csv&gid=0"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleRatingCsv)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Общий рейтинг", "0");

        Assert.Equal(SampleRatingCsv, result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenBothGvizAndFallbackThrowExceptions_ReturnsNullGracefully()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => throw new HttpRequestException("Total network outage"));

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Общий рейтинг", "0");

        Assert.Null(result);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SyncFromGoogleSheetsAsync_WhenOnlyOneRatingSheetSucceeds_SyncsSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.OriginalString;
            // Season fails entirely
            if (uri.Contains("646289371") || uri.Contains(Uri.EscapeDataString("Рейтинг сезона")))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Unauthorized")
                };
            }

            // Total succeeds via gviz
            if (uri.Contains(Uri.EscapeDataString("Общий рейтинг")))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
                        "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"449\",\"7,36\"\r\n")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.SyncFromGoogleSheetsAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.TotalProcessed);
        var user = await context.Users.FirstOrDefaultAsync();
        Assert.NotNull(user);
        Assert.Equal(449, user.TotalRating);
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_UsesCustomSpreadsheetIdWhenProvided()
    {
        using var context = CreateInMemoryDbContext();
        const string customId = "custom_test_sheet_12345";
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("a,b\r\n1,2")
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance, customId);

        await service.DownloadCsvWithFallbackAsync("Общий рейтинг", "0");

        Assert.Single(handler.Requests);
        Assert.Contains(customId, handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenGvizReturnsGoogleVisualizationError_FallsBackToGidExport()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("gviz/tq"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("/*O_o*/\ngoogle.visualization.Query.setResponse({\"version\":\"0.6\",\"status\":\"error\",\"errors\":[{\"reason\":\"not_found\",\"message\":\"Requested sheet not found\"}]});")
                };
            }

            if (req.RequestUri!.ToString().Contains("export?format=csv&gid=0"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleRatingCsv)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Общий рейтинг", "0");

        Assert.Equal(SampleRatingCsv, result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("export?format=csv&gid=0", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenGvizReturnsJsonError_FallsBackToGidExport()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri!.ToString().Contains("gviz/tq"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":\"error\",\"message\":\"Sheet not found\"}")
                };
            }

            if (req.RequestUri!.ToString().Contains("export?format=csv&gid=646289371"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleRatingCsv)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Рейтинг сезона", "646289371");

        Assert.Equal(SampleRatingCsv, result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("export?format=csv&gid=646289371", handler.Requests[1].RequestUri!.ToString());
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenGidNotProvided_AutoResolvesKnownGidForSeasonAndTotal()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            // Gviz fails
            if (uri.Contains("gviz/tq"))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("Unauthorized")
                };
            }

            if (uri.Contains("export?format=csv&gid=646289371"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("season_csv_data")
                };
            }

            if (uri.Contains("export?format=csv&gid=0"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("total_csv_data")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        // Call without providing gid parameter
        var seasonResult = await service.DownloadCsvWithFallbackAsync("Рейтинг сезона");
        var totalResult = await service.DownloadCsvWithFallbackAsync("Общий рейтинг");

        Assert.Equal("season_csv_data", seasonResult);
        Assert.Equal("total_csv_data", totalResult);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Contains("export?format=csv&gid=646289371", handler.Requests[1].RequestUri!.ToString());
        Assert.Contains("export?format=csv&gid=0", handler.Requests[3].RequestUri!.ToString());
    }

    [Fact]
    public async Task GoogleSheetsSyncService_WhenConfigurationHasCustomSpreadsheetId_ReadsFromConfiguration()
    {
        using var context = CreateInMemoryDbContext();
        const string customConfigId = "config_sheet_id_99999";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GoogleSheets:SpreadsheetId", customConfigId }
            })
            .Build();

        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("col1\r\nval1")
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance, config);

        await service.DownloadCsvWithFallbackAsync("Общий рейтинг", "0");

        Assert.Single(handler.Requests);
        Assert.Contains(customConfigId, handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GoogleSheetsSyncService_WhenConfigurationHasCustomRegistrationsGid_UsesRegistrationsGidFallback()
    {
        using var context = CreateInMemoryDbContext();
        const string customRegGid = "777888999";
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GoogleSheets:RegistrationsGid", customRegGid }
            })
            .Build();

        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.OriginalString;
            if (uri.Contains("gviz/tq"))
            {
                // Rating sheets succeed via gviz, registrations fails via gviz
                if (uri.Contains("РЕГИСТРАЦИИ", StringComparison.OrdinalIgnoreCase) || 
                    uri.Contains(Uri.EscapeDataString("РЕГИСТРАЦИИ"), StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Unauthorized") };
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRatingCsv) };
            }

            if (uri.Contains($"export?format=csv&gid={customRegGid}"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleRegistrationsCsv) };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance, config);

        var result = await service.SyncFromGoogleSheetsAsync();

        Assert.True(result.Success);
        // Fallback for registrations succeeded and populated club card IDs
        var user = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Гуляев");
        Assert.NotNull(user);
        Assert.Equal("1345", user.ClubCardId);
    }

    [Theory]
    [InlineData("a,b,c\r\n1,2,3", true)]
    [InlineData("\"Место\",\"Игрок\"\r\n1,\"Иванов\"", true)]
    [InlineData("<!DOCTYPE html><html><body>Login</body></html>", false)]
    [InlineData("<html><head><title>Sign in</title></head></html>", false)]
    [InlineData("   <div class=\"login\">Sign In</div>", false)]
    [InlineData("/*O_o*/\ngoogle.visualization.Query.setResponse({...});", false)]
    [InlineData("{\"status\":\"error\",\"message\":\"Not found\"}", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsValidCsvContent_ValidatesCorrectlyAcrossFormats(string content, bool expectedValid)
    {
        var isValid = GoogleSheetsSyncService.IsValidCsvContent(content);
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public async Task AdminController_SyncSheets_WhenSuccessful_ReturnsOk()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleRatingCsv)
        });

        using var httpClient = new HttpClient(handler);
        var syncService = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);
        var controller = new AdminController(syncService);

        var actionResult = await controller.SyncSheets();

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var result = Assert.IsType<GoogleSheetsSyncResult>(okResult.Value);
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalProcessed);
    }

    [Fact]
    public async Task AdminController_SyncGoogleSheets_WhenSuccessful_ReturnsOk()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleRatingCsv)
        });

        using var httpClient = new HttpClient(handler);
        var syncService = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);
        var controller = new AdminController(syncService);

        var actionResult = await controller.SyncGoogleSheets(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var result = Assert.IsType<GoogleSheetsSyncResult>(okResult.Value);
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalProcessed);
    }

    [Fact]
    public async Task AdminController_SyncGoogleSheets_WhenFailed_ReturnsBadRequestWithErrorResult()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("Unauthorized")
        });

        using var httpClient = new HttpClient(handler);
        var syncService = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);
        var controller = new AdminController(syncService);

        var actionResult = await controller.SyncGoogleSheets(CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var result = Assert.IsType<GoogleSheetsSyncResult>(badRequestResult.Value);
        Assert.False(result.Success);
        Assert.Equal("Не удалось загрузить ни лист «Рейтинг сезона», ни «Общий рейтинг».", result.Message);
    }

    [Fact]
    public async Task AdminController_SyncGoogleSheets_IgnoresAbortedRequestCancellationToken_AndSucceeds()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleRatingCsv)
        });

        using var httpClient = new HttpClient(handler);
        var syncService = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);
        var controller = new AdminController(syncService);

        using var abortedCts = new CancellationTokenSource();
        abortedCts.Cancel();

        var actionResult = await controller.SyncGoogleSheets(abortedCts.Token);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var result = Assert.IsType<GoogleSheetsSyncResult>(okResult.Value);
        Assert.True(result.Success);
        Assert.Equal(3, result.TotalProcessed);
    }

    [Fact]
    public async Task GoogleSheetsSyncService_ConcurrentSyncCalls_ReturnsAlreadySyncingResult()
    {
        using var context = CreateInMemoryDbContext();
        var tcsFirstRequestStarted = new TaskCompletionSource<bool>();
        var tcsAllowFirstRequestToComplete = new TaskCompletionSource<bool>();

        var handler = new TestHttpMessageHandler(req =>
        {
            tcsFirstRequestStarted.TrySetResult(true);
            tcsAllowFirstRequestToComplete.Task.Wait();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleRatingCsv)
            };
        });

        using var httpClient = new HttpClient(handler);
        var syncService = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var firstTask = Task.Run(() => syncService.SyncFromGoogleSheetsAsync());
        await tcsFirstRequestStarted.Task;

        var secondResult = await syncService.SyncFromGoogleSheetsAsync();

        tcsAllowFirstRequestToComplete.SetResult(true);
        var firstResult = await firstTask;

        Assert.True(secondResult.Success);
        Assert.Equal(0, secondResult.TotalProcessed);
        Assert.Equal("Синхронизация уже выполняется в фоновом режиме", secondResult.Message);
        Assert.True(firstResult.Success);
    }

    [Fact]
    public void ServiceCollection_CanResolveGoogleSheetsSyncService_WithSocketsHttpHandlerAndUserAgent()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=test;Username=test;Password=test" },
                { "GoogleSheets:SpreadsheetId", "di_test_sheet_id" }
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();

        services.AddHttpClient<IGoogleSheetsSyncService, GoogleSheetsSyncService>(httpClient =>
        {
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoogleSheetsSyncService>();

        Assert.NotNull(service);
        var concrete = Assert.IsType<GoogleSheetsSyncService>(service);
        Assert.NotNull(concrete);
    }

    [Fact]
    public void ServiceCollection_CanResolveGoogleSheetsSyncService_WithoutCustomConfig_UsesDefaults()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();

        services.AddHttpClient<IGoogleSheetsSyncService, GoogleSheetsSyncService>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IGoogleSheetsSyncService>();

        Assert.NotNull(service);
        Assert.IsType<GoogleSheetsSyncService>(service);
    }
}
