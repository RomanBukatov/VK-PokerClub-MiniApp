using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PokerClub.Api.Controllers;
using PokerClub.Api.DTOs;
using PokerClub.Domain.Constants;
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

    [Theory]
    [InlineData("Чугаев Михаил,,,,\r\nВыбранный сезон:,ТЕХНИЧЕСКОЕ ОТКРЫТИЕ,,,,\r\n,,,,\r\nМесто,Игрок", "ТЕХНИЧЕСКОЕ ОТКРЫТИЕ")]
    [InlineData("Чугаев Михаил,,,,\r\nВыбранный сезон:,Осень 2026,,,,\r\n,,,,\r\nМесто,Игрок", "Осень 2026")]
    [InlineData("\"Выбранный сезон: Место\",\"ТЕХНИЧЕСКОЕ ОТКРЫТИЕ Игрок\",\"Турниров\"", "ТЕХНИЧЕСКОЕ ОТКРЫТИЕ")]
    [InlineData("\"Выбранный сезон: Место\",\"Осень 2026 Игрок\",\"Турниров\"", "Осень 2026")]
    [InlineData(",\r\n,\r\n,Осень 2026\r\n1,Игрок", "Осень 2026")]
    [InlineData(",\r\n,Заголовок B2\r\n,Осень 2026 (B3)\r\n1,Игрок", "Осень 2026 (B3)")]
    [InlineData("Сезон: Зима 2026\r\n1,Игрок", "Зима 2026")]
    [InlineData("Сезон Весна 2027\r\n1,Игрок", "Весна 2027")]
    [InlineData(",\r\n,\r\n,Сезон:\u00A0Лето\u00A02026\r\n1,Игрок", "Лето 2026")]
    [InlineData(",\r\n,\r\n,Выбранный сезон\r\n1,Игрок", "Осень 2026")]
    [InlineData("", "Осень 2026")]
    [InlineData(null, "Осень 2026")]
    public void ExtractSeasonName_CorrectlyExtractsSeasonOrDefaults(string? csvContent, string expected)
    {
        var result = GoogleSheetsSyncService.ExtractSeasonName(csvContent);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetLeaderboard_IncludesSeasonNameInResponseDto()
    {
        using var context = CreateInMemoryDbContext();
        context.Users.Add(new User { VkId = "u1", FirstName = "Иван", SeasonRating = 100 });
        await context.SaveChangesAsync();

        var ratingService = new RatingService(context);
        var controller = new RatingsController(ratingService);

        var result = await controller.GetLeaderboard();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<LeaderboardResponseDto>(ok.Value);

        Assert.NotNull(dto.SeasonName);
        Assert.Equal("Осень 2026", dto.SeasonName);
    }

    [Fact]
    public async Task SyncFromCsv_WhenAutumnSeasonHasNoGamesOrZeroPoints_SetsSeasonRatingZeroAndPreservesTotalRating()
    {
        using var context = CreateInMemoryDbContext();
        var existingUser = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 449,
            SeasonRating = 486,
            TournamentsPlayed = 22
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string emptyAutumnSeasonCsv =
            "\"РЕЙТИНГ ОСЕННЕГО СЕЗОНА 2026\",,,,,,,,\r\n" +
            "\"Турниры сезона «AUTUMN SEASON 2026»\",,,,,,,,\r\n" +
            ",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n";

        const string newTotalRatingCsv =
            "\"ОБЩИЙ РЕЙТИНГ MONTE CARLO\",,,,,,,,\r\n" +
            "\"Все участники и все проведённые турниры клуба\",,,,,,,,\r\n" +
            ",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"25\",\"3\",\"6\",\"12\",\"52\",\"593\",\"7,06\"\r\n" +
            "\"2\",\"Гуляев Игорь\",\"28\",\"1\",\"6\",\"10\",\"28\",\"495\",\"5,62\"\r\n";

        var result = await service.SyncFromCsvAsync(emptyAutumnSeasonCsv, newTotalRatingCsv, null);

        Assert.True(result.Success);
        Assert.Equal(2, result.TotalProcessed);

        var lukashenko = await context.Users.FirstAsync(u => u.VkId == "vk_lukashenko");
        Assert.Equal(0, lukashenko.SeasonRating);
        Assert.Equal(593, lukashenko.TotalRating);
        Assert.Equal(25, lukashenko.TournamentsPlayed);

        var gulyaev = await context.Users.FirstAsync(u => u.LastName == "Гуляев");
        Assert.Equal(0, gulyaev.SeasonRating);
        Assert.Equal(495, gulyaev.TotalRating);
        Assert.Equal(28, gulyaev.TournamentsPlayed);

        Assert.Equal("Осень 2026", GoogleSheetsSyncService.CurrentSeasonName);
    }

    [Fact]
    public async Task SyncFromCsv_WhenAutumnSeasonHasRowsWithZeroPoints_SetsSeasonRatingZeroAndPreservesTotalRating()
    {
        using var context = CreateInMemoryDbContext();
        var existingUser = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 449,
            SeasonRating = 486
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string zeroPointsAutumnSeasonCsv =
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"0\",\"0\",\"0\",\"0\",\"0\",\"0\",\"0,00\"\r\n";

        const string totalRatingCsv =
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"25\",\"3\",\"6\",\"12\",\"52\",\"593\",\"7,06\"\r\n";

        var result = await service.SyncFromCsvAsync(zeroPointsAutumnSeasonCsv, totalRatingCsv, null);

        Assert.True(result.Success);

        var lukashenko = await context.Users.FirstAsync(u => u.VkId == "vk_lukashenko");
        Assert.Equal(0, lukashenko.SeasonRating);
        Assert.Equal(593, lukashenko.TotalRating);
    }

    [Fact]
    public async Task GoogleSheetsSyncService_ReadsSpreadsheetIdAndSeasonGidFromConfiguration()
    {
        using var context = CreateInMemoryDbContext();
        const string expectedSheetId = "1zxU_LOSjIsjrEHw7eq366BQMLWQ4x8pSDIiMdUxWPOs";
        const string expectedSeasonGid = "202622";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GOOGLE_SHEETS_SPREADSHEET_ID", expectedSheetId },
                { "GOOGLE_SHEETS_SEASON_GID", expectedSeasonGid }
            })
            .Build();

        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri.Contains("export?format=csv&gid=202622"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("\"РЕЙТИНГ ОСЕННЕГО СЕЗОНА 2026\"\r\n\"Место\",\"Игрок\"\r\n")
                };
            }

            if (uri.Contains("export?format=csv&gid=0"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("\"ОБЩИЙ РЕЙТИНГ\"\r\n\"1\",\"Лукашенко Василий\",\"25\",\"3\",\"6\",\"12\",\"52\",\"593\",\"7,06\"\r\n")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance, config);

        var result = await service.SyncFromGoogleSheetsAsync();

        Assert.True(result.Success);
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().Contains(expectedSheetId));
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().Contains($"gid={expectedSeasonGid}"));

        var user = await context.Users.FirstOrDefaultAsync();
        Assert.NotNull(user);
        Assert.Equal(0, user.SeasonRating);
        Assert.Equal(593, user.TotalRating);
    }

    [Fact]
    public async Task SyncFromCsv_WhenUserNotInTotalRatingSheet_ResetsSeasonRatingToZeroAndPreservesTotalRating()
    {
        using var context = CreateInMemoryDbContext();
        var inactiveUser = new User
        {
            VkId = "vk_inactive",
            FirstName = "Иван",
            LastName = "Иванов",
            TotalRating = 300,
            SeasonRating = 150
        };
        var activeUser = new User
        {
            VkId = "vk_active",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 449,
            SeasonRating = 486
        };
        context.Users.AddRange(inactiveUser, activeUser);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string emptyAutumnCsv =
            "\"РЕЙТИНГ ОСЕННЕГО СЕЗОНА 2026\",,,,,,,,\r\n" +
            "\"Турниры сезона «AUTUMN SEASON 2026»\",,,,,,,,\r\n" +
            ",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n";

        const string totalRatingCsv =
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"25\",\"3\",\"6\",\"12\",\"52\",\"593\",\"7,06\"\r\n";

        var result = await service.SyncFromCsvAsync(emptyAutumnCsv, totalRatingCsv, null);

        Assert.True(result.Success);

        var refreshedInactive = await context.Users.FirstAsync(u => u.VkId == "vk_inactive");
        Assert.Equal(0, refreshedInactive.SeasonRating);
        Assert.Equal(300, refreshedInactive.TotalRating);

        var refreshedActive = await context.Users.FirstAsync(u => u.VkId == "vk_active");
        Assert.Equal(0, refreshedActive.SeasonRating);
        Assert.Equal(593, refreshedActive.TotalRating);
    }

    [Fact]
    public async Task SyncFromCsv_WhenAutumnSeasonHasActivePoints_SetsPointsForSeasonPlayersAndZeroForOthers()
    {
        using var context = CreateInMemoryDbContext();
        var lukashenko = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 449,
            SeasonRating = 486
        };
        var gulyaev = new User
        {
            VkId = "vk_gulyaev",
            FirstName = "Игорь",
            LastName = "Гуляев",
            TotalRating = 400,
            SeasonRating = 300
        };
        context.Users.AddRange(lukashenko, gulyaev);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string autumnSeasonCsv =
            "\"РЕЙТИНГ ОСЕННЕГО СЕЗОНА 2026\",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"1\",\"1\",\"1\",\"1\",\"5\",\"75\",\"1,00\"\r\n";

        const string totalRatingCsv =
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"26\",\"4\",\"7\",\"13\",\"57\",\"668\",\"6,80\"\r\n" +
            "\"2\",\"Гуляев Игорь\",\"28\",\"1\",\"6\",\"10\",\"28\",\"495\",\"5,62\"\r\n";

        var result = await service.SyncFromCsvAsync(autumnSeasonCsv, totalRatingCsv, null);

        Assert.True(result.Success);

        var refreshedLukashenko = await context.Users.FirstAsync(u => u.VkId == "vk_lukashenko");
        Assert.Equal(75, refreshedLukashenko.SeasonRating);
        Assert.Equal(668, refreshedLukashenko.TotalRating);

        var refreshedGulyaev = await context.Users.FirstAsync(u => u.VkId == "vk_gulyaev");
        Assert.Equal(0, refreshedGulyaev.SeasonRating);
        Assert.Equal(495, refreshedGulyaev.TotalRating);
    }

    [Fact]
    public async Task SyncFromGoogleSheetsAsync_WithLiveAutumnGvizFormat_CorrectlySyncsLeaderboard()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri.Contains("gviz/tq") && (uri.Contains("Осенний сезон 2026") || uri.Contains("202622") || uri.Contains("%D0%9E%D1%81%D0%B5%D0%BD%D0%BD%D0%B8%D0%B9")))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "\"РЕЙТИНГ ОСЕННЕГО СЕЗОНА 2026\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\"\r\n" +
                        "\"Турниры сезона «AUTUMN SEASON 2026»\",\"\",\"\",\"\",\"\",\"\",\"\",\"\",\"\"\r\n")
                };
            }

            if (uri.Contains("gviz/tq") && (uri.Contains("Общий рейтинг") || uri.Contains("%D0%9E%D0%B1%D1%89%D0%B8%D0%B9")))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "\"ОБЩИЙ РЕЙТИНГ MONTE CARLO Все участники и все проведённые турниры клуба Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Очков\",\"Среднее место\"\r\n" +
                        "\"1\",\"Лукашенко Василий\",\"25\",\"3\",\"6\",\"12\",\"52\",\"593\",\"7,06\"\r\n")
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
        Assert.Equal(0, user.SeasonRating);
        Assert.Equal(593, user.TotalRating);
        Assert.Equal("Осень 2026", GoogleSheetsSyncService.CurrentSeasonName);
    }

    [Fact]
    public void ParseRatingSheet_LukashenkoVasiliyRow_ParsesTournamentsWinsAndRatingCorrectly()
    {
        const string csvRow = "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"";
        var rows = GoogleSheetsSyncService.ParseRatingSheet(csvRow);

        Assert.Single(rows);
        var stats = rows[0];
        Assert.Equal(1, stats.Place);
        Assert.Equal("Лукашенко Василий", stats.PlayerName);
        Assert.Equal(25, stats.TournamentsPlayed);
        Assert.Equal(3, stats.WinsCount);
        Assert.Equal(6, stats.Top3Count);
        Assert.Equal(12, stats.Top10Count);
        Assert.Equal(52, stats.KnockoutsCount);
        Assert.Equal(593, stats.Points);
        Assert.Equal(7.06, stats.AvgPlace);
    }

    [Fact]
    public async Task SyncFromCsv_LukashenkoVasiliyRow_ParsesTournamentsWinsAndRatingCorrectly_AndClubCardIsNot25()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string totalRatingCsv =
            "\"ОБЩИЙ РЕЙТИНГ MONTE CARLO\",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"\r\n" +
            "2,Гуляев Игорь,28,1,6,10,28,495,\"5,62\"\r\n" +
            "3,Заббаров Марат,24,2,7,11,35,463,\"6,10\"\r\n";

        var result = await service.SyncFromCsvAsync(totalRatingCsv);

        Assert.True(result.Success);

        var lukashenko = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Лукашенко" && u.FirstName == "Василий");
        Assert.NotNull(lukashenko);
        Assert.Equal(25, lukashenko.TournamentsPlayed);
        Assert.Equal(3, lukashenko.WinsCount);
        Assert.Equal(6, lukashenko.Top3Count);
        Assert.Equal(12, lukashenko.Top10Count);
        Assert.Equal(52, lukashenko.KnockoutsCount);
        Assert.Equal(593, lukashenko.TotalRating);
        Assert.Equal(7.06, lukashenko.AvgPlace);
        Assert.NotEqual("25", lukashenko.ClubCardId);
        Assert.Null(lukashenko.ClubCardId);
        Assert.NotEqual("3", lukashenko.PhoneNumber);
        Assert.Null(lukashenko.PhoneNumber);

        var gulyaev = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Гуляев" && u.FirstName == "Игорь");
        Assert.NotNull(gulyaev);
        Assert.Equal(495, gulyaev.TotalRating);

        var zabbarov = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Заббаров");
        Assert.NotNull(zabbarov);
        Assert.Equal(463, zabbarov.TotalRating);
    }

    [Fact]
    public async Task SyncFromCsv_LukashenkoWithRegistrations_AssignsRealCardAndDoesNotOverwriteWith25()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string totalRatingCsv =
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"\r\n";

        const string registrationsCsv =
            "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"Комментарий\"\r\n" +
            "\"16 сентября | 19:00 FREEROLL\",\"Лукашенко Василий\",\"1060\",\"89027909924\",\"\"\r\n";

        var result = await service.SyncFromCsvAsync(null, totalRatingCsv, registrationsCsv);

        Assert.True(result.Success);
        var lukashenko = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Лукашенко" && u.FirstName == "Василий");
        Assert.NotNull(lukashenko);
        Assert.Equal(25, lukashenko.TournamentsPlayed);
        Assert.Equal(3, lukashenko.WinsCount);
        Assert.Equal(593, lukashenko.TotalRating);
        Assert.NotEqual("25", lukashenko.ClubCardId);
        Assert.Equal("1060", lukashenko.ClubCardId);
        Assert.Equal("89027909924", lukashenko.PhoneNumber);
    }

    [Fact]
    public async Task SyncFromCsv_WhenAnotherUserHasCard25And73Points_DoesNotOverwriteLukashenko()
    {
        using var context = CreateInMemoryDbContext();
        var player25 = new User
        {
            VkId = "vk_player_25",
            FirstName = "Иван",
            LastName = "Двадцатьпятый",
            ClubCardId = "25",
            TotalRating = 73
        };
        var lukashenko = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            ClubCardId = "1060",
            TotalRating = 0
        };
        context.Users.AddRange(player25, lukashenko);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string totalRatingCsv =
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"\r\n";

        var result = await service.SyncFromCsvAsync(totalRatingCsv);

        Assert.True(result.Success);

        var refreshedLukashenko = await context.Users.FirstAsync(u => u.VkId == "vk_lukashenko");
        Assert.Equal(593, refreshedLukashenko.TotalRating);
        Assert.Equal(25, refreshedLukashenko.TournamentsPlayed);
        Assert.Equal(3, refreshedLukashenko.WinsCount);
        Assert.Equal("1060", refreshedLukashenko.ClubCardId);

        var refreshedPlayer25 = await context.Users.FirstAsync(u => u.VkId == "vk_player_25");
        Assert.Equal(73, refreshedPlayer25.TotalRating);
        Assert.Equal("25", refreshedPlayer25.ClubCardId);
    }

    [Fact]
    public void ParseRegistrationsMapping_WhenGivenRatingSheetRow_DoesNotParseTournamentsAndWinsAsCardAndPhone()
    {
        const string ratingRowAsCsv =
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"\r\n";

        var mapping = GoogleSheetsSyncService.ParseRegistrationsMapping(ratingRowAsCsv);

        Assert.Empty(mapping);
    }

    [Fact]
    public async Task SyncFromCsv_WhenLukashenkoHadCorruptedCard25AndPhone3InDb_ClearsCorruptedCardAndPhoneAndSets593Points()
    {
        using var context = CreateInMemoryDbContext();
        var corruptedLukashenko = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            ClubCardId = "25", // Falsely set from TournamentsPlayed in old sync
            PhoneNumber = "3", // Falsely set from WinsCount in old sync
            TotalRating = 73,  // Falsely overwritten in old sync
            TournamentsPlayed = 25,
            WinsCount = 3
        };
        context.Users.Add(corruptedLukashenko);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string totalRatingCsv =
            "\"ОБЩИЙ РЕЙТИНГ MONTE CARLO\",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"\r\n";

        var result = await service.SyncFromCsvAsync(totalRatingCsv);

        Assert.True(result.Success);

        var refreshed = await context.Users.FirstAsync(u => u.VkId == "vk_lukashenko");
        Assert.Equal(593, refreshed.TotalRating);
        Assert.Equal(25, refreshed.TournamentsPlayed);
        Assert.Equal(3, refreshed.WinsCount);
        Assert.NotEqual("25", refreshed.ClubCardId);
        Assert.Null(refreshed.ClubCardId);
        Assert.NotEqual("3", refreshed.PhoneNumber);
        Assert.Null(refreshed.PhoneNumber);
    }

    [Fact]
    public async Task SyncFromCsv_WhenLukashenkoHadCorruptedCard25AndPhone3InDb_WithRegistrations_SetsRealCard1060AndRealPhone()
    {
        using var context = CreateInMemoryDbContext();
        var corruptedLukashenko = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            ClubCardId = "25",
            PhoneNumber = "3",
            TotalRating = 73,
            TournamentsPlayed = 25,
            WinsCount = 3
        };
        context.Users.Add(corruptedLukashenko);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string totalRatingCsv =
            "\"ОБЩИЙ РЕЙТИНГ MONTE CARLO\",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"\r\n";

        const string registrationsCsv =
            "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"Комментарий\"\r\n" +
            "\"16 сентября | 19:00 FREEROLL\",\"Лукашенко Василий\",\"1060\",\"89027909924\",\"\"\r\n";

        var result = await service.SyncFromCsvAsync(null, totalRatingCsv, registrationsCsv);

        Assert.True(result.Success);

        var refreshed = await context.Users.FirstAsync(u => u.VkId == "vk_lukashenko");
        Assert.Equal(593, refreshed.TotalRating);
        Assert.Equal(25, refreshed.TournamentsPlayed);
        Assert.Equal(3, refreshed.WinsCount);
        Assert.NotEqual("25", refreshed.ClubCardId);
        Assert.Equal("1060", refreshed.ClubCardId);
        Assert.NotEqual("3", refreshed.PhoneNumber);
        Assert.Equal("89027909924", refreshed.PhoneNumber);
    }

    [Fact]
    public async Task SyncFromCsv_WhenPlayer25ExistsInRegistrationsAndRating_AndLukashenkoHadCard25InDb_DoesNotOverwriteLukashenko()
    {
        using var context = CreateInMemoryDbContext();
        var corruptedLukashenko = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            ClubCardId = "25", // Corrupted card matching player 25
            PhoneNumber = "3",
            TotalRating = 73,
            TournamentsPlayed = 25,
            WinsCount = 3
        };
        context.Users.Add(corruptedLukashenko);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string totalRatingCsv =
            "\"ОБЩИЙ РЕЙТИНГ MONTE CARLO\",,,,,,,,\r\n" +
            "\"Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП‑3\",\"ТОП‑10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "1,Лукашенко Василий,25,3,6,12,52,593,\"7,06\"\r\n" +
            "25,Иванов Иван,10,1,2,5,12,73,\"15,50\"\r\n";

        const string registrationsCsv =
            "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"Комментарий\"\r\n" +
            "\"16 сентября | 19:00 FREEROLL\",\"Лукашенко Василий\",\"1060\",\"89027909924\",\"\"\r\n" +
            "\"16 сентября | 19:00 FREEROLL\",\"Иванов Иван\",\"25\",\"89001234567\",\"\"\r\n";

        var result = await service.SyncFromCsvAsync(null, totalRatingCsv, registrationsCsv);

        Assert.True(result.Success);

        var refreshedLukashenko = await context.Users.FirstAsync(u => u.VkId == "vk_lukashenko");
        Assert.Equal(593, refreshedLukashenko.TotalRating);
        Assert.Equal(25, refreshedLukashenko.TournamentsPlayed);
        Assert.Equal(3, refreshedLukashenko.WinsCount);
        Assert.Equal("1060", refreshedLukashenko.ClubCardId);
        Assert.Equal("89027909924", refreshedLukashenko.PhoneNumber);

        var ivanov = await context.Users.FirstAsync(u => u.LastName == "Иванов" && u.FirstName == "Иван");
        Assert.Equal(73, ivanov.TotalRating);
        Assert.Equal("25", ivanov.ClubCardId);
        Assert.Equal("89001234567", ivanov.PhoneNumber);
    }

    [Fact]
    public void ParseRegistrationsMapping_WithDuplicateRowsForSamePlayer_MergesCardAndPhone()
    {
        const string registrationsCsv =
            "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"Комментарий\"\r\n" +
            "\"15 сентября\",\"Лукашенко Василий\",\"1060\",\"\",\"\"\r\n" +
            "\"16 сентября\",\"Лукашенко Василий\",\"\",\"89027909924\",\"\"\r\n";

        var mapping = GoogleSheetsSyncService.ParseRegistrationsMapping(registrationsCsv);

        Assert.True(mapping.TryGetValue("василий_лукашенко", out var info));
        Assert.Equal("1060", info.ClubCardId);
        Assert.Equal("89027909924", info.PhoneNumber);
    }

    [Fact]
    public void ParseRegistrationsMapping_WithDummyCardValues_IgnoresDummyCards()
    {
        const string registrationsCsvWithPhones =
            "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"Комментарий\"\r\n" +
            "\"15 сентября\",\"Сидоров Петр\",\"-\",\"89001112233\",\"\"\r\n" +
            "\"16 сентября\",\"Петров Сидор\",\"нет\",\"89002223344\",\"\"\r\n" +
            "\"17 сентября\",\"Иванов Сидор\",\"б/н\",\"89003334455\",\"\"\r\n";

        var mapping = GoogleSheetsSyncService.ParseRegistrationsMapping(registrationsCsvWithPhones);

        Assert.Equal(3, mapping.Count);
        Assert.All(mapping.Values, info => Assert.Null(info.ClubCardId));
        Assert.Equal("89001112233", mapping["петр_сидоров"].PhoneNumber);

        const string registrationsCsvWithoutPhones =
            "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"Комментарий\"\r\n" +
            "\"15 сентября\",\"Сидоров Петр\",\"-\",\"\",\"\"\r\n" +
            "\"16 сентября\",\"Петров Сидор\",\"нет\",\"\",\"\"\r\n" +
            "\"17 сентября\",\"Иванов Сидор\",\"б/н\",\"\",\"\"\r\n";

        var emptyMapping = GoogleSheetsSyncService.ParseRegistrationsMapping(registrationsCsvWithoutPhones);
        Assert.Empty(emptyMapping);
    }

    [Fact]
    public void ParsePlayersSheet_ValidAndBlockedCards_FiltersCorrectly()
    {
        const string playersCsv =
            "\"ID игрока\",\"ФИО\",\"Телефон\",\"Дата\",\"Статус\"\r\n" +
            "\"101\",\"Иванов Иван Иванович\",\"89001112233\",\"01.01.2025\",\"Активна\"\r\n" +
            "\"102\",\"Петров Петр\",\"89002223344\",\"02.01.2025\",\"\"\r\n" +
            "\"103\",\"Сидоров Сидор\",\"89003334455\",\"03.01.2025\",\"Заблокирована\"\r\n" +
            "\"104\",\"Смирнов Алексей\",\"89004445566\",\"04.01.2025\",\"Утеряна\"\r\n" +
            "\"105\",\"Кузнецов Михаил\",\"89005556677\",\"05.01.2025\",\"Неактивна\"\r\n" +
            "\"106\",\"Федорова Анна\",\"89006667788\",\"06.01.2025\",\"Бан\"\r\n";

        var parsed = GoogleSheetsSyncService.ParsePlayersSheet(playersCsv);

        Assert.Equal(2, parsed.Count);
        Assert.Contains(parsed, p => p.CardId == "101" && p.FullName == "Иванов Иван Иванович");
        Assert.Contains(parsed, p => p.CardId == "102" && p.FullName == "Петров Петр");
    }

    [Fact]
    public async Task SyncFromCsv_PreservesExactSheetRankFromColumn0()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string ratingCsv =
            "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "\"157\",\"Федотова Евгения\",\"1\",\"0\",\"0\",\"0\",\"0\",\"10\",\"15,00\"\r\n";

        var result = await service.SyncFromCsvAsync(ratingCsv);
        Assert.True(result.Success);

        var user = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Федотова");
        Assert.NotNull(user);
        Assert.Equal(157, user.SheetRank);
        Assert.Equal(10, user.TotalRating);
    }

    [Fact]
    public async Task SyncFromCsv_WithPlayersSheet_ImportsCardsAndCreatesWhitelistHolders()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string ratingCsv =
            "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"486\",\"7,36\"\r\n";

        const string playersCsv =
            "\"ID игрока\",\"ФИО\",\"Телефон\",\"Дата\",\"Статус\"\r\n" +
            "\"1060\",\"Лукашенко Василий\",\"89027909924\",\"01.01.2025\",\"Активна\"\r\n" +
            "\"2001\",\"Новый Игрок Белого Списка\",\"\",\"01.01.2025\",\"\"\r\n";

        var result = await service.SyncFromCsvAsync(ratingCsv, null, null, playersCsv);
        Assert.True(result.Success);

        // Лукашенко получил карту 1060 из листа «Игроки»
        var lukashenko = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Лукашенко");
        Assert.NotNull(lukashenko);
        Assert.Equal("1060", lukashenko.ClubCardId);
        Assert.Equal(1, lukashenko.SheetRank);

        // Для карты 2001 создан плейсхолдер белого списка
        var holder = await context.Users.FirstOrDefaultAsync(u => u.ClubCardId == "2001");
        Assert.NotNull(holder);
        Assert.StartsWith("sheet_card_2001_", holder.VkId);
    }

    [Fact]
    public void ParsePlayersSheet_WithSingleColumnCardId_ParsesCardCorrectly()
    {
        const string csv =
            "\"ID игрока\",\"ФИО\",\"Телефон\",\"Дата\",\"Статус\"\r\n" +
            "\"3050\"\r\n" +
            "\"3051\",\"Петров Петр\"\r\n";

        var parsed = GoogleSheetsSyncService.ParsePlayersSheet(csv);
        Assert.Equal(2, parsed.Count);
        Assert.Contains(parsed, p => p.CardId == "3050" && p.FullName == "");
        Assert.Contains(parsed, p => p.CardId == "3051" && p.FullName == "Петров Петр");
    }

    [Fact]
    public void ParsePlayersSheet_WithBlockedAndAnnulledStatus_ExcludesBlockedCards()
    {
        const string csv =
            "\"ID игрока\",\"ФИО\",\"Телефон\",\"Дата\",\"Статус\"\r\n" +
            "\"1001\",\"Игрок 1\",\"\",\"\",\"Активна\"\r\n" +
            "\"1002\",\"Игрок 2\",\"\",\"\",\"Заблокирована\"\r\n" +
            "\"1003\",\"Игрок 3\",\"\",\"\",\"Утеряна\"\r\n" +
            "\"1004\",\"Игрок 4\",\"\",\"\",\"Аннулирована\"\r\n" +
            "\"1005\",\"Игрок 5\",\"\",\"\",\"Не активна\"\r\n";

        var parsed = GoogleSheetsSyncService.ParsePlayersSheet(csv);
        Assert.Single(parsed);
        Assert.Equal("1001", parsed[0].CardId);
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenNonRatingSheetReturnsRatingSheetContent_ReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            // Google Sheets returns sheet 0 (Total Rating) when "Игроки" is requested
            if (uri.Contains("sheet=%D0%98%D0%B3%D1%80%D0%BE%D0%BA%D0%B8"))
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

        var result = await service.DownloadCsvWithFallbackAsync("Игроки");

        // Must reject rating content when requesting non-rating sheet and return null
        Assert.Null(result);
    }

    [Fact]
    public void ParsePlayersSheet_WhenGivenRatingSheetCsv_ReturnsEmptyListAndDoesNotParsePlacesAsCards()
    {
        var result = GoogleSheetsSyncService.ParsePlayersSheet(SampleRatingCsv);
        Assert.Empty(result);
    }

    [Fact]
    public async Task SyncFromCsvAsync_CleansCorruptedClubCardIdMatchingSheetRank()
    {
        using var context = CreateInMemoryDbContext();
        // User with corrupted ClubCardId matching SheetRank (e.g. rank 5 with card "5")
        var userWithCorruptedCard = new User
        {
            VkId = "sheet_5_corrupted",
            FirstName = "Игорь",
            LastName = "Гуляев",
            TotalRating = 504,
            SheetRank = 5,
            ClubCardId = "5",
            TournamentsPlayed = 26
        };
        context.Users.Add(userWithCorruptedCard);
        await context.SaveChangesAsync();

        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string ratingCsv = 
            "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "\"5\",\"Гуляев Игорь\",\"26\",\"1\",\"6\",\"10\",\"28\",\"504\",\"4,92\"\r\n";

        var result = await service.SyncFromCsvAsync(ratingCsv);
        Assert.True(result.Success);

        var updatedUser = await context.Users.FirstOrDefaultAsync(u => u.LastName == "Гуляев");
        Assert.NotNull(updatedUser);
        Assert.Equal(504, updatedUser.TotalRating);
        Assert.Equal(5, updatedUser.SheetRank);
        // Corrupted ClubCardId "5" must be reset to null!
        Assert.Null(updatedUser.ClubCardId);
    }

    [Theory]
    [InlineData("Телефон: 89221648828", "9221648828")]
    [InlineData("+7 904 847-31-61", "9048473161")]
    [InlineData("тел: 9221648828", "9221648828")]
    [InlineData("89027909924", "9027909924")]
    [InlineData("79027909924", "9027909924")]
    [InlineData("8 (922) 164-88-28", "9221648828")]
    [InlineData("+7(922)164-88-28", "9221648828")]
    [InlineData("тел. 8-922-164-88-28", "9221648828")]
    [InlineData("Постоянный гость. Телефон: 89221648828", "9221648828")]
    [InlineData("9221648828", "9221648828")]
    [InlineData("01.01.2025", null)]
    [InlineData("3", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ExtractAndNormalizePhoneNumber_WithVariousFormats_ExtractsNormalized10Digits(string? input, string? expected)
    {
        var result = GoogleSheetsSyncService.ExtractAndNormalizePhoneNumber(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParsePlayersSheet_CustomerPhotoFormat_ParsesCardsStatusAndPhoneCorrectly()
    {
        const string csv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1928\",\"Коба Анна\",\"01.01.2025\",\"\",\"Активен\",\"Телефон: 89221648828\"\r\n" +
            "\"1931\",\"Сидоров Сидор\",\"02.01.2025\",\"\",\"Заблокирован\",\"Телефон: 89001234567\"\r\n" +
            "\"1932\",\"Петров Петр\",\"03.01.2025\",\"\",\"Активна\",\"+7 904 847-31-61\"\r\n" +
            "\"1933\",\"Кузнецов Михаил\",\"04.01.2025\",\"\",\"Активен\",\"тел: 9221648828\"\r\n" +
            "\"1934\",\"Банный Бан\",\"05.01.2025\",\"\",\"Бан\",\"Телефон: 89009998877\"\r\n";

        var parsed = GoogleSheetsSyncService.ParsePlayersSheet(csv);

        Assert.Equal(3, parsed.Count);

        var koba = parsed.FirstOrDefault(p => p.CardId == "1928");
        Assert.NotNull(koba);
        Assert.Equal("Коба Анна", koba.FullName);
        Assert.Equal("9221648828", koba.PhoneNumber);

        var petrov = parsed.FirstOrDefault(p => p.CardId == "1932");
        Assert.NotNull(petrov);
        Assert.Equal("Петров Петр", petrov.FullName);
        Assert.Equal("9048473161", petrov.PhoneNumber);

        var kuznetsov = parsed.FirstOrDefault(p => p.CardId == "1933");
        Assert.NotNull(kuznetsov);
        Assert.Equal("Кузнецов Михаил", kuznetsov.FullName);
        Assert.Equal("9221648828", kuznetsov.PhoneNumber);

        // Blocked cards 1931 and 1934 must be omitted
        Assert.DoesNotContain(parsed, p => p.CardId == "1931");
        Assert.DoesNotContain(parsed, p => p.CardId == "1934");
    }

    [Fact]
    public async Task SyncFromCsvAsync_CustomerSheet_CreatesWhitelistStubsWithPhoneAndAllowsProfileBinding()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string playersCsv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1928\",\"Коба Анна\",\"01.01.2025\",\"\",\"Активен\",\"Телефон: 89221648828\"\r\n" +
            "\"1931\",\"Сидоров Сидор\",\"02.01.2025\",\"\",\"Заблокирован\",\"Телефон: 89001234567\"\r\n";

        var syncResult = await service.SyncFromCsvAsync(null, null, null, playersCsv);
        Assert.True(syncResult.Success);

        // 1. Убеждаемся, что в базе создан пользователь с ClubCardId = "1928"
        var cardHolder = await context.Users.FirstOrDefaultAsync(u => u.ClubCardId == "1928");
        Assert.NotNull(cardHolder);
        Assert.Equal("Анна", cardHolder.FirstName);
        Assert.Equal("Коба", cardHolder.LastName);
        Assert.Equal("9221648828", cardHolder.PhoneNumber);
        Assert.StartsWith("sheet_card_1928_", cardHolder.VkId);

        // 2. Убеждаемся, что заблокированная карта 1931 не была добавлена
        var blockedHolder = await context.Users.FirstOrDefaultAsync(u => u.ClubCardId == "1931");
        Assert.Null(blockedHolder);

        // 3. Тестируем привязку профиля через UsersController
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_anna_koba_real";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Успешная привязка с правильным телефоном
        var request = new UpdateProfileRequest(
            FullName: "Коба Анна",
            ClubCardId: "1928",
            PhoneNumber: "89221648828"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);
        Assert.Equal("1928", profile.ClubCardId);

        // Заглушка sheet_card_1928_... должна быть удалена, профиль привязан к vk_anna_koba_real
        var realUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "vk_anna_koba_real");
        Assert.NotNull(realUser);
        Assert.Equal("1928", realUser.ClubCardId);

        // 4. Попытка привязать заблокированную карту 1931 должна вернуть BadRequest
        var blockedRequest = new UpdateProfileRequest(
            FullName: "Неизвестный",
            ClubCardId: "1931",
            PhoneNumber: "89001234567"
        );
        var blockedResult = await controller.UpdateProfile(blockedRequest);
        Assert.IsType<BadRequestObjectResult>(blockedResult.Result);

        // 5. Попытка привязать несуществующую карту 999999 должна вернуть BadRequest
        var fakeRequest = new UpdateProfileRequest(
            FullName: "Фейк",
            ClubCardId: "999999"
        );
        var fakeResult = await controller.UpdateProfile(fakeRequest);
        Assert.IsType<BadRequestObjectResult>(fakeResult.Result);
    }

    [Fact]
    public async Task SyncFromCsvAsync_WhenPlayerAlreadyExistsInRating_LinksCardAndPhoneWithoutDuplicate()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        const string ratingCsv =
            "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
            "\"2\",\"Гуляев Игорь\",\"26\",\"1\",\"6\",\"10\",\"28\",\"485\",\"4,92\"\r\n";

        const string playersCsv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1345\",\"Гуляев Игорь\",\"01.01.2025\",\"\",\"Активен\",\"Телефон: 89223636110\"\r\n";

        var syncResult = await service.SyncFromCsvAsync(null, ratingCsv, null, playersCsv);
        Assert.True(syncResult.Success);

        // В базе должен быть ровно 1 пользователь — без создания дубликата
        var users = await context.Users.ToListAsync();
        Assert.Single(users);

        var gulyaev = users[0];
        Assert.Equal("Гуляев", gulyaev.LastName);
        Assert.Equal("Игорь", gulyaev.FirstName);
        Assert.Equal("1345", gulyaev.ClubCardId);
        Assert.Equal("9223636110", gulyaev.PhoneNumber);
        Assert.Equal(485, gulyaev.TotalRating);
        Assert.Equal(26, gulyaev.TournamentsPlayed);
    }

    [Fact]
    public async Task UsersController_MasterAdminCard_BypassesWhitelistAndGrantsAdmin()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_secret_admin_777";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Главный Администратор",
            ClubCardId: MasterClubCardConstants.MasterClubCardId
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(MasterClubCardConstants.MasterClubCardId, profile.ClubCardId);
        Assert.True(profile.IsAdmin);

        var secondaryRequest = new UpdateProfileRequest(
            FullName: "Резервный Администратор",
            ClubCardId: MasterClubCardConstants.SecondaryMasterClubCardId
        );
        var secondaryResult = await controller.UpdateProfile(secondaryRequest);
        var secondaryOk = Assert.IsType<OkObjectResult>(secondaryResult.Result);
        var secondaryProfile = Assert.IsType<UserProfileDto>(secondaryOk.Value);
        Assert.True(secondaryProfile.IsAdmin);
    }

    [Fact]
    public void IsPlayersSheetCsv_ValidCustomerAndLegacyFormats_ReturnsTrue()
    {
        const string customerPhotoCsv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1928\",\"Коба Анна\",\"01.01.2025\",\"\",\"Активен\",\"Телефон: 89221648828\"\r\n";
        Assert.True(GoogleSheetsSyncService.IsPlayersSheetCsv(customerPhotoCsv));

        const string legacyCsv =
            "\"ID игрока\",\"ФИО\",\"Телефон\",\"Дата\",\"Статус\"\r\n" +
            "\"101\",\"Иванов Иван\",\"89001112233\",\"01.01.2025\",\"Активна\"\r\n";
        Assert.True(GoogleSheetsSyncService.IsPlayersSheetCsv(legacyCsv));
    }

    [Fact]
    public void IsPlayersSheetCsv_OtherSheets_ReturnsFalse()
    {
        // 1. Rating sheet
        Assert.False(GoogleSheetsSyncService.IsPlayersSheetCsv(SampleRatingCsv));

        // 2. Registrations sheet
        const string regCsv =
            "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"БАЙ-ИН\"\r\n" +
            "\"12.01.2025\",\"Иван Иванов\",\"101\",\"89001112233\",\"1000\"\r\n";
        Assert.False(GoogleSheetsSyncService.IsPlayersSheetCsv(regCsv));

        // 3. Tournament schedule
        const string scheduleCsv =
            "\"Турнир\",\"Дата\",\"Время\",\"Бай-ин\",\"Гарантия\"\r\n" +
            "\"Sunday Special\",\"15.01.2025\",\"19:00\",\"2500\",\"100000\"\r\n";
        Assert.False(GoogleSheetsSyncService.IsPlayersSheetCsv(scheduleCsv));

        // 4. Null / whitespace
        Assert.False(GoogleSheetsSyncService.IsPlayersSheetCsv(""));
        Assert.False(GoogleSheetsSyncService.IsPlayersSheetCsv("   "));
        Assert.False(GoogleSheetsSyncService.IsPlayersSheetCsv(null));
    }

    [Fact]
    public async Task DownloadCsvWithFallbackAsync_WhenPlayersSheetReturnsAnotherSheet_RejectsAndReturnsNull()
    {
        using var context = CreateInMemoryDbContext();
        var handler = new TestHttpMessageHandler(req =>
        {
            var uri = req.RequestUri!.ToString();
            // Google Sheets returns registrations or schedule when "Игроки" is requested
            if (uri.Contains("sheet=%D0%98%D0%B3%D1%80%D0%BE%D0%BA%D0%B8"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "\"ДАТА / ТУРНИР\",\"ИМЯ\",\"ID\",\"ТЕЛЕФОН\",\"БАЙ-ИН\"\r\n" +
                        "\"12.01.2025\",\"Иван Иванов\",\"101\",\"89001112233\",\"1000\"\r\n")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        var result = await service.DownloadCsvWithFallbackAsync("Игроки");
        Assert.Null(result);
    }

    [Fact]
    public void ParsePlayersSheet_WhenPhoneIsInCol3_ExtractsPhoneCorrectly()
    {
        const string csv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1935\",\"Смирнов Иван\",\"01.01.2025\",\"+7 922 164-88-28\",\"Активен\",\"\"\r\n" +
            "\"1936\",\"Попов Алексей\",\"02.01.2025\",\"89048473161\",\"Активен\",\"Постоянный гость\"\r\n";

        var parsed = GoogleSheetsSyncService.ParsePlayersSheet(csv);
        Assert.Equal(2, parsed.Count);

        var smirnov = parsed.FirstOrDefault(p => p.CardId == "1935");
        Assert.NotNull(smirnov);
        Assert.Equal("9221648828", smirnov.PhoneNumber);

        var popov = parsed.FirstOrDefault(p => p.CardId == "1936");
        Assert.NotNull(popov);
        Assert.Equal("9048473161", popov.PhoneNumber);
    }

    [Fact]
    public void ParsePlayersSheet_WhenCommentHasDatesAndCardNumbersWithoutPhone_DoesNotExtractFakePhone()
    {
        const string csv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1937\",\"Орлов Денис\",\"01.01.2025\",\"\",\"Активен\",\"Выдана 01.01.2025, карта №1937\"\r\n";

        var parsed = GoogleSheetsSyncService.ParsePlayersSheet(csv);
        Assert.Single(parsed);
        Assert.Null(parsed[0].PhoneNumber);
    }

    [Fact]
    public async Task SyncFromCsvAsync_WhenCardIsBlockedInNewSync_RemovesOldStubUser()
    {
        using var context = CreateInMemoryDbContext();
        using var httpClient = new HttpClient();
        var service = new GoogleSheetsSyncService(context, httpClient, NullLogger<GoogleSheetsSyncService>.Instance);

        // 1. Первый синк: карта 1940 активна
        const string initialPlayersCsv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1940\",\"Тестов Тест\",\"01.01.2025\",\"89001234567\",\"Активен\",\"\"\r\n";

        var res1 = await service.SyncFromCsvAsync(null, null, null, initialPlayersCsv);
        Assert.True(res1.Success);

        var stub1940 = await context.Users.FirstOrDefaultAsync(u => u.ClubCardId == "1940");
        Assert.NotNull(stub1940);

        // 2. Второй синк: администратор заблокировал карту 1940
        const string updatedPlayersCsv =
            "\"ID игрока\",\"ФИО\",\"Дата\",\"Телефон\",\"Статус\",\"Комментарий\"\r\n" +
            "\"1940\",\"Тестов Тест\",\"01.01.2025\",\"89001234567\",\"Заблокирован\",\"\"\r\n";

        var res2 = await service.SyncFromCsvAsync(null, null, null, updatedPlayersCsv);
        Assert.True(res2.Success);

        // Заглушка заблокированной карты должна быть удалена из базы!
        var blockedStub = await context.Users.FirstOrDefaultAsync(u => u.ClubCardId == "1940");
        Assert.Null(blockedStub);
    }
}


