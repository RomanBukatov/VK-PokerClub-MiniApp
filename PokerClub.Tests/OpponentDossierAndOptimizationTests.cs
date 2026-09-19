using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PokerClub.Api.Controllers;
using PokerClub.Api.DTOs;
using PokerClub.Api.Services;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;
using Xunit;

namespace PokerClub.Tests;

public class OpponentDossierAndOptimizationTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_ReturnsExpectedFieldsAndExcludesPrivateData()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "100500",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerKing",
            ClubCardId = "MC-777",
            PhoneNumber = "+79991234567",
            AcceptedTermsAt = DateTime.UtcNow,
            SeasonRating = 450,
            TotalRating = 1250,
            TournamentsPlayed = 15,
            WinsCount = 3,
            Top3Count = 6,
            Top10Count = 11,
            KnockoutsCount = 18,
            AvgPlace = 4.25,
            AvatarUrl = "https://example.com/avatar.jpg",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);

        var actionResult = await controller.GetPublicProfile(user.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<PublicUserProfileDto>(okResult.Value);

        Assert.Equal(user.Id, dto.Id);
        Assert.Equal("PokerKing", dto.Nickname);
        Assert.Equal("Иван", dto.FirstName);
        Assert.Equal("Иванов", dto.LastName);
        Assert.Equal(450, dto.SeasonRating);
        Assert.Equal(1250, dto.TotalRating);
        Assert.Equal(15, dto.TournamentsPlayed);
        Assert.Equal(3, dto.WinsCount);
        Assert.Equal(6, dto.Top3Count);
        Assert.Equal(11, dto.Top10Count);
        Assert.Equal(18, dto.KnockoutsCount);
        Assert.Equal(4.25, dto.AvgPlace);
        Assert.Equal("https://example.com/avatar.jpg", dto.AvatarUrl);
        Assert.Equal("100500", dto.VkId);

        // Verify that for regular player, ClubCardId and PhoneNumber are null
        Assert.Null(dto.ClubCardId);
        Assert.Null(dto.PhoneNumber);

        // Verify that AcceptedTermsAt is still absent from DTO, but PhoneNumber and ClubCardId exist on DTO
        var dtoType = typeof(PublicUserProfileDto);
        Assert.Null(dtoType.GetProperty("AcceptedTermsAt"));
        Assert.NotNull(dtoType.GetProperty("PhoneNumber"));
        Assert.NotNull(dtoType.GetProperty("ClubCardId"));
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WhenAdmin_ReturnsClubCardIdAndPhoneNumber()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "100500",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerKing",
            ClubCardId = "MC-777",
            PhoneNumber = "+79991234567",
            AcceptedTermsAt = DateTime.UtcNow,
            SeasonRating = 450,
            TotalRating = 1250,
            TournamentsPlayed = 15,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Is-Admin"] = "true";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetPublicProfile(user.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<PublicUserProfileDto>(okResult.Value);

        Assert.Equal("MC-777", dto.ClubCardId);
        Assert.Equal("+79991234567", dto.PhoneNumber);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WhenOwner_ReturnsClubCardIdAndPhoneNumber()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "100500",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerKing",
            ClubCardId = "MC-777",
            PhoneNumber = "+79991234567",
            AcceptedTermsAt = DateTime.UtcNow,
            SeasonRating = 450,
            TotalRating = 1250,
            TournamentsPlayed = 15,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "100500";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetPublicProfile(user.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<PublicUserProfileDto>(okResult.Value);

        Assert.Equal("MC-777", dto.ClubCardId);
        Assert.Equal("+79991234567", dto.PhoneNumber);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WhenAnotherRegularUser_ReturnsNullForClubCardIdAndPhoneNumber()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "100500",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerKing",
            ClubCardId = "MC-777",
            PhoneNumber = "+79991234567",
            AcceptedTermsAt = DateTime.UtcNow,
            SeasonRating = 450,
            TotalRating = 1250,
            TournamentsPlayed = 15,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "999999";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetPublicProfile(user.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<PublicUserProfileDto>(okResult.Value);

        Assert.Null(dto.ClubCardId);
        Assert.Null(dto.PhoneNumber);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WhenAdminByConfiguredVkId_ReturnsClubCardIdAndPhoneNumber()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "100500",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerKing",
            ClubCardId = "MC-777",
            PhoneNumber = "+79991234567",
            AcceptedTermsAt = DateTime.UtcNow,
            SeasonRating = 450,
            TotalRating = 1250,
            TournamentsPlayed = 15,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new PokerClub.Api.Models.VkOptions
        {
            AdminVkIds = new List<string> { "admin_user_42" }
        });
        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var controller = new UsersController(context, NullLogger<UsersController>.Instance, validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "admin_user_42";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetPublicProfile(user.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<PublicUserProfileDto>(okResult.Value);

        Assert.Equal("MC-777", dto.ClubCardId);
        Assert.Equal("+79991234567", dto.PhoneNumber);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WhenAdminSwitchesToPlayerModeWithXIsAdminFalse_ReturnsNullForOpponentData()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "100500",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerKing",
            ClubCardId = "MC-777",
            PhoneNumber = "+79991234567",
            AcceptedTermsAt = DateTime.UtcNow,
            SeasonRating = 450,
            TotalRating = 1250,
            TournamentsPlayed = 15,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var options = Microsoft.Extensions.Options.Options.Create(new PokerClub.Api.Models.VkOptions
        {
            AdminVkIds = new List<string> { "admin_user_42" }
        });
        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var controller = new UsersController(context, NullLogger<UsersController>.Instance, validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "admin_user_42";
        httpContext.Request.Headers["X-Is-Admin"] = "false";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetPublicProfile(user.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<PublicUserProfileDto>(okResult.Value);

        // Switched to player mode -> should not see opponent's sensitive data
        Assert.Null(dto.ClubCardId);
        Assert.Null(dto.PhoneNumber);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WhenOwnerWithXIsAdminFalse_StillReturnsOwnClubCardIdAndPhoneNumber()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "100500",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerKing",
            ClubCardId = "MC-777",
            PhoneNumber = "+79991234567",
            AcceptedTermsAt = DateTime.UtcNow,
            SeasonRating = 450,
            TotalRating = 1250,
            TournamentsPlayed = 15,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "100500";
        httpContext.Request.Headers["X-Is-Admin"] = "false";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetPublicProfile(user.Id.ToString());
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<PublicUserProfileDto>(okResult.Value);

        // Even with X-Is-Admin: false, the user is the owner of this profile
        Assert.Equal("MC-777", dto.ClubCardId);
        Assert.Equal("+79991234567", dto.PhoneNumber);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WhenNotFound_ReturnsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);

        var actionResult = await controller.GetPublicProfile("999999");
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task UsersController_GetPublicProfile_WorksWithBothDbIdAndVkId()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "vk_unique_888",
            FirstName = "Алексей",
            LastName = "Смирнов",
            SeasonRating = 100,
            TotalRating = 200,
            TournamentsPlayed = 2,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);

        // Find by integer DB ID
        var byDbIdResult = await controller.GetPublicProfile(user.Id.ToString());
        var okByDbId = Assert.IsType<OkObjectResult>(byDbIdResult.Result);
        var dto1 = Assert.IsType<PublicUserProfileDto>(okByDbId.Value);
        Assert.Equal(user.Id, dto1.Id);

        // Find by string VK ID
        var byVkIdResult = await controller.GetPublicProfile("vk_unique_888");
        var okByVkId = Assert.IsType<OkObjectResult>(byVkIdResult.Result);
        var dto2 = Assert.IsType<PublicUserProfileDto>(okByVkId.Value);
        Assert.Equal(user.Id, dto2.Id);
    }

    [Fact]
    public async Task RatingsController_Leaderboard_CachesResponseAndInvalidatesOnTokenReset()
    {
        var mockRatingService = new CountingRatingService();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheResetToken = new LeaderboardCacheResetToken();

        var controller = new RatingsController(mockRatingService, memoryCache, cacheResetToken);

        // First call - should query rating service
        var res1 = await controller.GetLeaderboard(limit: 50, offset: 0, type: "season");
        var ok1 = Assert.IsType<OkObjectResult>(res1.Result);
        Assert.Equal(1, mockRatingService.CallCount);

        // Second call with same parameters - should return cached value without querying rating service
        var res2 = await controller.GetLeaderboard(limit: 50, offset: 0, type: "season");
        var ok2 = Assert.IsType<OkObjectResult>(res2.Result);
        Assert.Equal(1, mockRatingService.CallCount);

        // Invalidate cache
        cacheResetToken.Reset();

        // Third call - cache invalidated, should query rating service again
        var res3 = await controller.GetLeaderboard(limit: 50, offset: 0, type: "season");
        var ok3 = Assert.IsType<OkObjectResult>(res3.Result);
        Assert.Equal(2, mockRatingService.CallCount);
    }

    [Fact]
    public async Task GoogleSheetsBackgroundSyncService_ExecutesAndResetsCacheOnSuccess()
    {
        var services = new ServiceCollection();
        var mockSyncService = new MockGoogleSheetsSyncService(shouldSucceed: true);
        services.AddSingleton<IGoogleSheetsSyncService>(mockSyncService);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheResetToken = new LeaderboardCacheResetToken();

        // Populate cache entry
        memoryCache.Set("leaderboard_season_50_0", "cached_leaderboard", cacheResetToken.GetExpirationToken());
        Assert.True(memoryCache.TryGetValue("leaderboard_season_50_0", out _));

        var syncWorker = new GoogleSheetsBackgroundSyncService(
            scopeFactory,
            NullLogger<GoogleSheetsBackgroundSyncService>.Instance,
            cacheResetToken,
            memoryCache,
            TimeSpan.FromMilliseconds(50));

        // Test manual ResetLeaderboardCache
        syncWorker.ResetLeaderboardCache();
        Assert.False(memoryCache.TryGetValue("leaderboard_season_50_0", out _));
    }

    [Fact]
    public async Task GoogleSheetsBackgroundSyncService_HandlesNetworkExceptionGracefully()
    {
        var services = new ServiceCollection();
        var mockSyncService = new MockGoogleSheetsSyncService(throwNetworkError: true);
        services.AddSingleton<IGoogleSheetsSyncService>(mockSyncService);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var syncWorker = new GoogleSheetsBackgroundSyncService(
            scopeFactory,
            NullLogger<GoogleSheetsBackgroundSyncService>.Instance,
            new LeaderboardCacheResetToken(),
            new MemoryCache(new MemoryCacheOptions()),
            TimeSpan.FromMilliseconds(20));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(80);

        // Must not throw uncaught exception
        var executeTask = syncWorker.StartAsync(cts.Token);
        await Task.Delay(100);
        await syncWorker.StopAsync(CancellationToken.None);

        Assert.True(mockSyncService.CallCount > 0);
    }

    [Fact]
    public void AppDbContext_ConfiguresDescendingIndexesForRatings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);
        var designModel = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions
            .GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>(context).Model;
        var entityType = designModel.FindEntityType(typeof(User));
        Assert.NotNull(entityType);

        var seasonIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "IX_Users_SeasonRating_Desc");
        Assert.NotNull(seasonIndex);
        Assert.Equal("IX_Users_SeasonRating_Desc", seasonIndex.GetDatabaseName());

        var totalIndex = entityType.GetIndexes()
            .FirstOrDefault(i => i.GetDatabaseName() == "IX_Users_TotalRating_Desc");
        Assert.NotNull(totalIndex);
        Assert.Equal("IX_Users_TotalRating_Desc", totalIndex.GetDatabaseName());
    }

    [Fact]
    public async Task RatingsController_AssignPoints_InvalidatesLeaderboardCache()
    {
        var mockRatingService = new CountingRatingService();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheResetToken = new LeaderboardCacheResetToken();

        var controller = new RatingsController(mockRatingService, memoryCache, cacheResetToken);

        // Populate cache entry
        memoryCache.Set("leaderboard_season_50_0", "cached_leaderboard", cacheResetToken.GetExpirationToken());
        Assert.True(memoryCache.TryGetValue("leaderboard_season_50_0", out _));

        var req = new AssignPointsRequest(TournamentId: 1, UserPoints: new Dictionary<int, int> { { 1, 50 } });
        var res = await controller.AssignPoints(req);
        Assert.IsType<OkObjectResult>(res);

        // Cache must be cleared after points assignment
        Assert.False(memoryCache.TryGetValue("leaderboard_season_50_0", out _));
    }

    [Fact]
    public async Task GoogleSheetsSyncService_ResetsCacheTokenOnSync()
    {
        using var context = CreateInMemoryDbContext();
        var cacheResetToken = new LeaderboardCacheResetToken();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());

        memoryCache.Set("leaderboard_season_50_0", "cached_leaderboard", cacheResetToken.GetExpirationToken());
        Assert.True(memoryCache.TryGetValue("leaderboard_season_50_0", out _));

        var syncService = new GoogleSheetsSyncService(
            context,
            new HttpClient(),
            NullLogger<GoogleSheetsSyncService>.Instance,
            "dummy_id",
            null,
            cacheResetToken);

        var csv = "\"ОБЩИЙ РЕЙТИНГ КЛУБА Место\",\"Игрок\",\"Турниров\",\"Побед\",\"ТОП-3\",\"ТОП-10\",\"Нокаутов\",\"Сумма очков\",\"Среднее место\"\r\n" +
                  "\"1\",\"Лукашенко Василий\",\"22\",\"2\",\"5\",\"10\",\"40\",\"486\",\"7,36\"\r\n";
        var result = await syncService.SyncFromCsvAsync(csv);
        Assert.True(result.Success, result.Message);

        // Token must have been reset, invalidating memory cache
        Assert.False(memoryCache.TryGetValue("leaderboard_season_50_0", out _));
    }

    private class CountingRatingService : IRatingService
    {
        public int CallCount { get; private set; }

        public Task<(List<User> Users, int TotalCount)> GetLeaderboardAsync(int limit = 50, int offset = 0, string type = "season")
        {
            CallCount++;
            var list = new List<User>
            {
                new User { Id = 1, VkId = "vk1", FirstName = "P1", SeasonRating = 100, TotalRating = 200 }
            };
            return Task.FromResult((list, 1));
        }

        public Task<(bool Success, string Message)> AssignPointsAndFinishTournamentAsync(int tournamentId, Dictionary<int, int> userPoints)
        {
            return Task.FromResult((true, "OK"));
        }
    }

    private class MockGoogleSheetsSyncService : IGoogleSheetsSyncService
    {
        private readonly bool _shouldSucceed;
        private readonly bool _throwNetworkError;
        public int CallCount { get; private set; }

        public MockGoogleSheetsSyncService(bool shouldSucceed = true, bool throwNetworkError = false)
        {
            _shouldSucceed = shouldSucceed;
            _throwNetworkError = throwNetworkError;
        }

        public Task<GoogleSheetsSyncResult> SyncFromGoogleSheetsAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_throwNetworkError)
            {
                throw new HttpRequestException("Simulated network outage");
            }

            return Task.FromResult(new GoogleSheetsSyncResult(
                _shouldSucceed,
                TotalProcessed: 10,
                UpdatedCount: 5,
                CreatedCount: 2,
                Message: _shouldSucceed ? "Success" : "Failed"));
        }

        public Task<GoogleSheetsSyncResult> SyncAsync(CancellationToken cancellationToken = default)
            => SyncFromGoogleSheetsAsync(cancellationToken);

        public Task<GoogleSheetsSyncResult> SyncFromCsvAsync(string csvContent, CancellationToken cancellationToken = default)
            => SyncFromGoogleSheetsAsync(cancellationToken);

        public Task<GoogleSheetsSyncResult> SyncFromCsvAsync(string ratingCsvContent, string? registrationsCsvContent, CancellationToken cancellationToken = default)
            => SyncFromGoogleSheetsAsync(cancellationToken);

        public Task<GoogleSheetsSyncResult> SyncFromCsvAsync(string? seasonRatingCsvContent, string? totalRatingCsvContent, string? registrationsCsvContent, CancellationToken cancellationToken = default)
            => SyncFromGoogleSheetsAsync(cancellationToken);
    }
}
