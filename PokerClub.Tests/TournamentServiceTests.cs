using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PokerClub.Api.Controllers;
using PokerClub.Api.DTOs;
using PokerClub.Api.Filters;
using PokerClub.Api.Models;
using PokerClub.Api.Services;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;
using Xunit;

namespace PokerClub.Tests;

public class TournamentServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateTournament_WithoutClubId_AutoLinksFirstAvailableClub()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var startTime = DateTime.SpecifyKind(new DateTime(2026, 9, 8, 19, 0, 0), DateTimeKind.Unspecified);

        var (success, tournament, message) = await service.CreateTournamentAsync(
            clubId: null,
            title: "Friday Deepstack",
            format: "NL Holdem",
            buyIn: 1500,
            maxSeats: 30,
            startTime: startTime,
            description: "Test description"
        );

        Assert.True(success);
        Assert.NotNull(tournament);
        Assert.Equal(club.Id, tournament.ClubId);
        Assert.Equal(DateTimeKind.Utc, tournament.StartTime.Kind);
        Assert.Equal(TournamentStatus.RegistrationOpen, tournament.Status);
    }

    [Fact]
    public async Task CreateTournament_WithTextAddress_UpdatesClubAddress()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Старый адрес", City = city, IsActive = true };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var startTime = new DateTime(2026, 9, 8, 19, 0, 0, DateTimeKind.Utc);

        var (success, tournament, message) = await service.CreateTournamentAsync(
            clubId: club.Id,
            title: "Friday Deepstack",
            format: "NL Holdem",
            buyIn: 1500,
            maxSeats: 30,
            startTime: startTime,
            description: "Test description",
            address: "Куйбышева 7"
        );

        Assert.True(success);
        Assert.NotNull(tournament);
        Assert.Equal("Куйбышева 7", tournament.Club?.Address);

        var updatedClub = await context.Clubs.FindAsync(club.Id);
        Assert.Equal("Куйбышева 7", updatedClub?.Address);
    }

    [Fact]
    public async Task CreateTournament_WhenNoClubsExist_CreatesDefaultClubAndCity()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TournamentService(context);
        var startTime = new DateTime(2026, 9, 8, 19, 0, 0, DateTimeKind.Utc);

        var (success, tournament, message) = await service.CreateTournamentAsync(
            clubId: null,
            title: "Freeroll",
            format: "NL Holdem",
            buyIn: 0,
            maxSeats: 40,
            startTime: startTime,
            description: "Default creation test",
            address: "Куйбышева 7"
        );

        Assert.True(success);
        Assert.NotNull(tournament);
        Assert.NotNull(tournament.Club);
        Assert.Equal("Куйбышева 7", tournament.Club.Address);
        Assert.True(tournament.ClubId > 0);
    }

    [Fact]
    public async Task GetSchedule_WhenIncludeFinishedTrue_ReturnsAllTournaments()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        context.Cities.Add(city);
        context.Clubs.Add(club);

        var tourActive = new Tournament
        {
            Club = club,
            Title = "Freeroll Tournament",
            Status = TournamentStatus.RegistrationOpen,
            StartTime = DateTime.UtcNow.AddHours(2)
        };
        var tourAnnounced = new Tournament
        {
            Club = club,
            Title = "Sunday Grand",
            Status = TournamentStatus.Announced,
            StartTime = DateTime.UtcNow.AddDays(1)
        };
        var tourFinished = new Tournament
        {
            Club = club,
            Title = "Weekly Club Cup",
            Status = TournamentStatus.Finished,
            StartTime = DateTime.UtcNow.AddDays(-1)
        };

        context.Tournaments.AddRange(tourActive, tourAnnounced, tourFinished);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        // includeFinished = true: All 3 tournaments returned
        var allTournaments = await service.GetScheduleAsync(cityId: null, clubId: null, includeFinished: true);
        Assert.Equal(3, allTournaments.Count);
        Assert.Contains(allTournaments, t => t.Title == "Weekly Club Cup");
        Assert.Contains(allTournaments, t => t.Title == "Freeroll Tournament");
        Assert.Contains(allTournaments, t => t.Title == "Sunday Grand");

        // includeFinished = false: Only active and announced returned
        var activeTournaments = await service.GetScheduleAsync(cityId: null, clubId: null, includeFinished: false);
        Assert.Equal(2, activeTournaments.Count);
        Assert.DoesNotContain(activeTournaments, t => t.Title == "Weekly Club Cup");
    }

    [Fact]
    public async Task GetSchedule_WhenCityIdNull_ReturnsTournamentsFromAllCities()
    {
        using var context = CreateInMemoryDbContext();
        var city1 = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var city2 = new City { Name = "Екатеринбург", Slug = "ekb", IsActive = true };
        var club1 = new Club { Name = "Club 1", City = city1, IsActive = true };
        var club2 = new Club { Name = "Club 2", City = city2, IsActive = true };

        context.Cities.AddRange(city1, city2);
        context.Clubs.AddRange(club1, club2);

        var tour1 = new Tournament { Club = club1, Title = "Perm Tournament", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(1) };
        var tour2 = new Tournament { Club = club2, Title = "Ekb Tournament", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(2) };

        context.Tournaments.AddRange(tour1, tour2);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        // cityId = null -> all tournaments
        var allTournaments = await service.GetScheduleAsync(cityId: null, clubId: null, includeFinished: true);
        Assert.Equal(2, allTournaments.Count);

        // cityId = city1.Id -> only city 1
        var permTournaments = await service.GetScheduleAsync(cityId: city1.Id, clubId: null, includeFinished: true);
        Assert.Single(permTournaments);
        Assert.Equal("Perm Tournament", permTournaments[0].Title);
    }

    [Fact]
    public void VkAuthValidator_InDemoMode_OnlyGrantsAdminToTrustedIds()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);

        // Недоверенный ID не получает права админа даже в демо-режиме
        var httpContextNonAdmin = new DefaultHttpContext();
        httpContextNonAdmin.Request.Headers["X-Test-Vk-Id"] = "999999999";
        var resultNonAdmin = validator.Validate(httpContextNonAdmin);
        Assert.True(resultNonAdmin.IsValid);
        Assert.False(resultNonAdmin.IsAdmin);

        // Доверенный ID получает права админа
        var httpContextAdmin = new DefaultHttpContext();
        httpContextAdmin.Request.Headers["X-Test-Vk-Id"] = "123456789";
        var resultAdmin = validator.Validate(httpContextAdmin);
        Assert.True(resultAdmin.IsValid);
        Assert.True(resultAdmin.IsAdmin);
    }

    [Fact]
    public void VkAuthValidator_WithXIsAdminHeader_UntrustedUser_DoesNotGrantAdmin()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = true,
            ClientSecret = "secret",
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "555";
        httpContext.Request.Headers["X-Is-Admin"] = "true";

        var result = validator.Validate(httpContext);
        Assert.True(result.IsValid);
        Assert.False(result.IsAdmin); // Недоверенный пользователь не может получить админку через заголовок
    }

    [Fact]
    public void VkAuthValidator_WithXIsAdminHeader_TrustedUser_GrantsAdmin()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = true,
            ClientSecret = "secret",
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "123456789";
        httpContext.Request.Headers["X-Is-Admin"] = "true";

        var result = validator.Validate(httpContext);
        Assert.True(result.IsValid);
        Assert.True(result.IsAdmin);
    }

    [Fact]
    public void VkAuthValidator_InDemoMode_UntrustedUserWithXIsAdminTrue_DoesNotGrantAdmin()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "999999999";
        httpContext.Request.Headers["X-Is-Admin"] = "true";

        var result = validator.Validate(httpContext);
        Assert.True(result.IsValid);
        Assert.False(result.IsAdmin);
    }

    [Fact]
    public void VkAuthValidator_InProductionMode_TrustedUserWithXIsAdminFalse_DoesNotGrantAdmin()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = true,
            ClientSecret = "secret",
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "123456789";
        httpContext.Request.Headers["X-Is-Admin"] = "false";

        var result = validator.Validate(httpContext);
        Assert.True(result.IsValid);
        Assert.False(result.IsAdmin);
    }

    [Fact]
    public async Task TournamentsController_CreateTournament_ValidatesTitleAndStartTime()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        // 1. Пустой заголовок
        var requestEmptyTitle = new CreateTournamentRequest(
            Title: "",
            StartTime: DateTime.UtcNow.AddDays(1)
        );
        var badResult1 = await controller.CreateTournament(requestEmptyTitle);
        Assert.IsType<BadRequestObjectResult>(badResult1.Result);

        // 2. Default StartTime (0001-01-01)
        var requestDefaultTime = new CreateTournamentRequest(
            Title: "Valid Title",
            StartTime: default
        );
        var badResult2 = await controller.CreateTournament(requestDefaultTime);
        Assert.IsType<BadRequestObjectResult>(badResult2.Result);

        // 3. Валидный запрос с авто-подвязкой клуба
        var validRequest = new CreateTournamentRequest(
            Title: "New Mega Event",
            Address: "Ленина 10",
            StartTime: DateTime.UtcNow.AddDays(1),
            MaxSeats: 50,
            BuyIn: 2000
        );
        var okResult = await controller.CreateTournament(validRequest);
        var createdResult = Assert.IsType<CreatedAtActionResult>(okResult.Result);
        var dto = Assert.IsType<TournamentScheduleDto>(createdResult.Value);
        Assert.Equal("New Mega Event", dto.Title);
        Assert.True(dto.Id > 0);
    }

    [Fact]
    public async Task CreateTournament_WhenClubWithSameAddressExists_ReusesExistingClub()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club1 = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var club2 = new Club { Name = "Poker Spot", Address = "Куйбышева 7", City = city, IsActive = true };
        context.Cities.Add(city);
        context.Clubs.AddRange(club1, club2);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var startTime = new DateTime(2026, 9, 8, 19, 0, 0, DateTimeKind.Utc);

        var (success, tournament, message) = await service.CreateTournamentAsync(
            clubId: null,
            title: "Rebuy Night",
            format: "NL Holdem",
            buyIn: 1000,
            maxSeats: 30,
            startTime: startTime,
            description: "Test description",
            address: "Куйбышева 7"
        );

        Assert.True(success);
        Assert.NotNull(tournament);
        Assert.Equal(club2.Id, tournament.ClubId);
        // club1 address should NOT be changed
        var reloadedClub1 = await context.Clubs.FindAsync(club1.Id);
        Assert.Equal("Монастырская 59", reloadedClub1?.Address);
    }

    [Fact]
    public async Task CreateTournament_WhenCitySpecifiedWithoutClubs_CreatesClubInThatCity()
    {
        using var context = CreateInMemoryDbContext();
        var cityPerm = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var cityEkb = new City { Name = "Екатеринбург", Slug = "ekb", IsActive = true };
        var clubPerm = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = cityPerm, IsActive = true };
        context.Cities.AddRange(cityPerm, cityEkb);
        context.Clubs.Add(clubPerm);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var startTime = new DateTime(2026, 9, 8, 19, 0, 0, DateTimeKind.Utc);

        var (success, tournament, message) = await service.CreateTournamentAsync(
            clubId: null,
            title: "Ekb Open",
            format: "NL Holdem",
            buyIn: 2000,
            maxSeats: 40,
            startTime: startTime,
            description: "Tournament in Ekb",
            cityId: cityEkb.Id,
            address: "Ленина 15"
        );

        Assert.True(success);
        Assert.NotNull(tournament);
        Assert.NotNull(tournament.Club);
        Assert.Equal(cityEkb.Id, tournament.Club.CityId);
        Assert.Equal("Ленина 15", tournament.Club.Address);
    }

    [Fact]
    public void VkAuthValidator_WhenRegularQueryStringProvided_DoesNotTreatAsVkLaunchParams()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = true,
            ClientSecret = "secret"
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        // Regular query string with no vk_user_id or sign
        httpContext.Request.QueryString = new QueryString("?includeFinished=true&cityId=1");

        var result = validator.Validate(httpContext);
        // Should indicate missing VK launch params, not "sign parameter not found"
        Assert.False(result.IsValid);
        Assert.Equal("Параметры запуска VK отсутствуют.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetSchedule_IncludesRegistrationsUsers()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var user = new User { VkId = "777", FirstName = "Ivan", LastName = "Ivanov", TotalRating = 100 };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var tour = new Tournament
        {
            Club = club,
            Title = "Tour With Users",
            Status = TournamentStatus.RegistrationOpen,
            StartTime = DateTime.UtcNow.AddHours(1)
        };
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        context.Registrations.Add(new Registration
        {
            TournamentId = tour.Id,
            UserId = user.Id,
            Status = RegStatus.Active
        });
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var schedule = await service.GetScheduleAsync(null, null, true);

        Assert.Single(schedule);
        Assert.Single(schedule[0].Registrations);
        Assert.NotNull(schedule[0].Registrations.First().User);
        Assert.Equal("777", schedule[0].Registrations.First().User?.VkId);
    }

    [Fact]
    public void VkAuthValidator_InDemoMode_WhenXIsAdminIsFalse_ReturnsIsAdminFalse()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "123456789";
        httpContext.Request.Headers["X-Is-Admin"] = "false";

        var result = validator.Validate(httpContext);

        Assert.True(result.IsValid);
        Assert.Equal("123456789", result.VkUserId);
        Assert.False(result.IsAdmin);
    }

    [Fact]
    public void VkAuthValidator_InDemoMode_WhenXIsAdminIsTrue_ReturnsIsAdminTrue()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "123456789";
        httpContext.Request.Headers["X-Is-Admin"] = "true";

        var result = validator.Validate(httpContext);

        Assert.True(result.IsValid);
        Assert.Equal("123456789", result.VkUserId);
        Assert.True(result.IsAdmin);
    }

    [Fact]
    public void HttpContextExtensions_GetVkUserId_WhenXTestVkIdHeaderProvided_ReturnsVkId()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "987654321";

        var vkId = PokerClub.Api.Extensions.HttpContextExtensions.GetVkUserId(httpContext);

        Assert.Equal("987654321", vkId);
    }

    [Theory]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData(" false ")]
    public void VkAuthValidator_InDemoMode_WhenXIsAdminIsFalseCaseInsensitive_ReturnsIsAdminFalse(string headerValue)
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "123456789" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "123456789";
        httpContext.Request.Headers["X-Is-Admin"] = headerValue;

        var result = validator.Validate(httpContext);

        Assert.True(result.IsValid);
        Assert.Equal("123456789", result.VkUserId);
        Assert.False(result.IsAdmin);
    }

    [Fact]
    public void HttpContextExtensions_GetVkUserId_WhenXVkSignHeaderProvided_ReturnsParsedVkUserId()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-VK-Sign"] = "?vk_user_id=55555&vk_app_id=123&sign=dummy";

        var vkId = PokerClub.Api.Extensions.HttpContextExtensions.GetVkUserId(httpContext);

        Assert.Equal("55555", vkId);
    }

    [Fact]
    public void HttpContextExtensions_IsVkAdmin_WhenXIsAdminCaseInsensitive_ReturnsTrue()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Is-Admin"] = "True";

        var isAdmin = PokerClub.Api.Extensions.HttpContextExtensions.IsVkAdmin(httpContext);

        Assert.True(isAdmin);
    }

    [Fact]
    public void VkAuthValidator_StrictAdminVkIds_OnlyGrantsAdminToConfiguredIds()
    {
        // Настроены только конкретные администраторы, например ID 77777 и 88888
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "77777", "88888" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);

        // ID 123456789 больше НЕ является админом по умолчанию (если его нет в AdminVkIds)
        Assert.False(validator.IsAdmin("123456789"));
        Assert.False(validator.IsAdmin("1"));
        Assert.False(validator.IsAdmin("admin_vk_id"));
        Assert.False(validator.IsAdmin("random_guest"));

        // Только ID из списка получают права администратора
        Assert.True(validator.IsAdmin("77777"));
        Assert.True(validator.IsAdmin("88888"));
    }

    [Theory]
    [InlineData("111,222,333", new[] { "111", "222", "333" })]
    [InlineData("111; 222; 333", new[] { "111", "222", "333" })]
    [InlineData("111 222 333", new[] { "111", "222", "333" })]
    public void VkAdminIds_EnvironmentVariableFormat_ParsesCorrectly(string envValue, string[] expectedIds)
    {
        var ids = envValue.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

        var options = Options.Create(new VkOptions
        {
            AdminVkIds = ids
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);

        foreach (var expectedId in expectedIds)
        {
            Assert.True(validator.IsAdmin(expectedId));
        }

        Assert.False(validator.IsAdmin("unknown_id"));
    }

    [Fact]
    public async Task VkAuthorizeAttribute_RequireAdmin_WhenUserInAdminVkIds_AllowsAccessEvenIfXIsAdminFalse()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "308885723" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var services = new ServiceCollection();
        services.AddSingleton<IVkAuthValidator>(validator);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Headers["X-Test-Vk-Id"] = "308885723";
        httpContext.Request.Headers["X-Is-Admin"] = "false";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object()
        );

        var filter = new VkAuthorizeAttribute { RequireAdmin = true };
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled);
        Assert.Null(actionExecutingContext.Result);
        Assert.True((bool?)httpContext.Items["IsAdmin"]);
    }

    [Fact]
    public async Task VkAuthorizeAttribute_RequireAdmin_WhenUserNotInAdminVkIds_Returns403EvenIfXIsAdminTrue()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "308885723" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var services = new ServiceCollection();
        services.AddSingleton<IVkAuthValidator>(validator);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Headers["X-Test-Vk-Id"] = "999";
        httpContext.Request.Headers["X-Is-Admin"] = "true";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object()
        );

        var filter = new VkAuthorizeAttribute { RequireAdmin = true };
        bool nextCalled = false;

        await filter.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled);
        var objectResult = Assert.IsType<ObjectResult>(actionExecutingContext.Result);
        Assert.Equal(403, objectResult.StatusCode);
    }
}

