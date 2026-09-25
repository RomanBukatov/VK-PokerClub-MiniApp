using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
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
using PokerClub.Domain.Interfaces;
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
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
    public async Task GetScheduleAsync_AlwaysSortsTournamentsByStartTimeAscending()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo Perm", City = city, IsActive = true };
        context.Cities.Add(city);
        context.Clubs.Add(club);

        var now = DateTime.UtcNow;

        var tourLater = new Tournament
        {
            Club = club,
            Title = "Турнир 22 сентября",
            Status = TournamentStatus.RegistrationOpen,
            StartTime = now.AddDays(2)
        };
        var tourEarlier = new Tournament
        {
            Club = club,
            Title = "Турнир 21 сентября",
            Status = TournamentStatus.RegistrationOpen,
            StartTime = now.AddDays(1)
        };
        var tourPast = new Tournament
        {
            Club = club,
            Title = "Турнир 20 сентября (прошедший)",
            Status = TournamentStatus.Finished,
            StartTime = now.AddDays(-1)
        };

        // Добавляем в хаотичном порядке
        context.Tournaments.AddRange(tourLater, tourPast, tourEarlier);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        // 1. includeFinished = false: турнир 21 сентября идет первым, затем 22 сентября
        var schedule = await service.GetScheduleAsync(null, null, includeFinished: false);
        Assert.Equal(2, schedule.Count);
        Assert.Equal("Турнир 21 сентября", schedule[0].Title);
        Assert.Equal("Турнир 22 сентября", schedule[1].Title);

        // 2. includeFinished = true: прошедший 20 сентября идет первым, затем 21 сентября, затем 22 сентября
        var adminSchedule = await service.GetScheduleAsync(null, null, includeFinished: true);
        Assert.Equal(3, adminSchedule.Count);
        Assert.Equal("Турнир 20 сентября (прошедший)", adminSchedule[0].Title);
        Assert.Equal("Турнир 21 сентября", adminSchedule[1].Title);
        Assert.Equal("Турнир 22 сентября", adminSchedule[2].Title);
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

    [Fact]
    public async Task DeleteTournamentAsync_DeletesTournamentAndItsRegistrations()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament { Club = club, Title = "Tournament to Delete", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(2) };
        var user = new User { VkId = "12345", FirstName = "Ivan", LastName = "Ivanov" };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.Registrations.Add(new Registration { TournamentId = tour.Id, UserId = user.Id, Status = RegStatus.Active });
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var (success, message) = await service.DeleteTournamentAsync(tour.Id);

        Assert.True(success);
        Assert.Equal("Турнир успешно удален.", message);
        Assert.Null(await context.Tournaments.FindAsync(tour.Id));
        Assert.Empty(await context.Registrations.Where(r => r.TournamentId == tour.Id).ToListAsync());
    }

    [Fact]
    public async Task TournamentsController_DeleteTournament_ReturnsOk_WhenSuccessful()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament { Club = club, Title = "Tour For Controller Delete", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(2) };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        var result = await controller.DeleteTournament(tour.Id);
        Assert.IsType<OkObjectResult>(result);

        // Deleting non-existing tournament returns NotFound
        var notFoundResult = await controller.DeleteTournament(999999);
        Assert.IsType<NotFoundObjectResult>(notFoundResult);
    }

    [Fact]
    public async Task GetUserTournamentsAsync_ReturnsOnlyTournamentsWhereUserIsRegisteredAndNotCanceled()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour1 = new Tournament { Club = club, Title = "Tour Active", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(2) };
        var tour2 = new Tournament { Club = club, Title = "Tour Canceled", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(3) };
        var tour3 = new Tournament { Club = club, Title = "Tour Other Player Only", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(4) };

        var user1 = new User { VkId = "user1", FirstName = "User", LastName = "One" };
        var user2 = new User { VkId = "user2", FirstName = "User", LastName = "Two" };

        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.AddRange(tour1, tour2, tour3);
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        context.Registrations.AddRange(
            new Registration { TournamentId = tour1.Id, UserId = user1.Id, User = user1, Status = RegStatus.Active },
            new Registration { TournamentId = tour1.Id, UserId = user2.Id, User = user2, Status = RegStatus.Active },
            new Registration { TournamentId = tour2.Id, UserId = user1.Id, User = user1, Status = RegStatus.Canceled },
            new Registration { TournamentId = tour2.Id, UserId = user2.Id, User = user2, Status = RegStatus.Active },
            new Registration { TournamentId = tour3.Id, UserId = user2.Id, User = user2, Status = RegStatus.Active }
        );
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var myTournaments = await service.GetUserTournamentsAsync("user1");

        Assert.Single(myTournaments);
        Assert.Equal(tour1.Id, myTournaments[0].Id);
        Assert.Equal("Tour Active", myTournaments[0].Title);
    }

    [Fact]
    public async Task TournamentsController_GetMyTournaments_ReturnsCorrectRegistrationStatusAndCount()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour1 = new Tournament { Club = club, Title = "Tour Active", Status = TournamentStatus.RegistrationOpen, StartTime = DateTime.UtcNow.AddHours(2) };
        var user1 = new User { VkId = "user1", FirstName = "User", LastName = "One" };
        var user2 = new User { VkId = "user2", FirstName = "User", LastName = "Two" };

        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour1);
        context.Users.AddRange(user1, user2);
        await context.SaveChangesAsync();

        context.Registrations.AddRange(
            new Registration { TournamentId = tour1.Id, UserId = user1.Id, User = user1, Status = RegStatus.Active },
            new Registration { TournamentId = tour1.Id, UserId = user2.Id, User = user2, Status = RegStatus.Played }
        );
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "user1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetMyTournaments();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);

        Assert.Single(list);
        Assert.Equal(tour1.Id, list[0].Id);
        Assert.True(list[0].IsUserRegistered);
        // Both Active and Played registrations are counted (user1 + user2 = 2)
        Assert.Equal(2, list[0].RegisteredCount);
    }

    [Fact]
    public async Task TournamentsController_GetMyTournaments_WhenUnauthorized_ReturnsUnauthorized()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TournamentService(context);
        var controller = new TournamentsController(service);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetMyTournaments();
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateTournament_WithRegistrationEnd_PersistsUtcRegistrationEnd()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TournamentService(context);
        var startTime = new DateTime(2026, 9, 20, 19, 0, 0, DateTimeKind.Utc);
        var regEnd = new DateTime(2026, 9, 20, 18, 30, 0, DateTimeKind.Utc);

        var (success, tournament, message) = await service.CreateTournamentAsync(
            clubId: null,
            title: "Sunday Special",
            format: "NL Holdem",
            buyIn: 2000,
            maxSeats: 30,
            startTime: startTime,
            description: "Sunday event",
            registrationEnd: regEnd
        );

        Assert.True(success);
        Assert.NotNull(tournament);
        Assert.NotNull(tournament.RegistrationEnd);
        Assert.Equal(regEnd, tournament.RegistrationEnd.Value);
        Assert.Equal(DateTimeKind.Utc, tournament.RegistrationEnd.Value.Kind);

        // Verify in DB
        var saved = await context.Tournaments.FindAsync(tournament.Id);
        Assert.NotNull(saved?.RegistrationEnd);
        Assert.Equal(regEnd, saved.RegistrationEnd.Value);
    }

    [Fact]
    public async Task TournamentsController_CreateTournament_WithRegistrationEnd_ReturnsScheduleDtoWithRegistrationEnd()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TournamentService(context);
        var controller = new TournamentsController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var startTime = new DateTime(2026, 9, 25, 20, 0, 0, DateTimeKind.Utc);
        var regEnd = new DateTime(2026, 9, 25, 19, 45, 0, DateTimeKind.Utc);

        var request = new CreateTournamentRequest(
            Title: "Friday Knockout",
            Format: "Bounty",
            BuyIn: 3000,
            MaxSeats: 40,
            StartTime: startTime,
            RegistrationEnd: regEnd
        );

        var actionResult = await controller.CreateTournament(request);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var dto = Assert.IsType<TournamentScheduleDto>(createdResult.Value);

        Assert.NotNull(dto.RegistrationEnd);
        Assert.Equal(regEnd, dto.RegistrationEnd.Value);

        // Verify GetTournament also returns registrationEnd
        var getResult = await controller.GetTournament(dto.Id);
        var okResult = Assert.IsType<OkObjectResult>(getResult.Result);
        var detailDto = Assert.IsType<TournamentDetailDto>(okResult.Value);
        Assert.NotNull(detailDto.RegistrationEnd);
        Assert.Equal(regEnd, detailDto.RegistrationEnd.Value);
    }

    [Fact]
    public async Task RegisterPlayerAsync_WhenWebhookUrlConfigured_SendsWebhookWithCorrectPayload()
    {
        using var context = CreateInMemoryDbContext();
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = new City { Name = "Пермь", Slug = "perm" }, IsActive = true };
        var startTime = new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc);
        var tournament = new Tournament
        {
            Title = "Friday Deepstack",
            Club = club,
            StartTime = startTime,
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 30
        };
        var user = new User
        {
            VkId = "12345",
            FirstName = "Иван",
            LastName = "Иванов",
            Nickname = "PokerPro",
            ClubCardId = "1266",
            PhoneNumber = "+79991234567"
        };
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new TestHttpMessageHandler(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(mockHandler);

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheets:RegistrationWebhookUrl"] = "https://script.google.com/macros/s/test/exec"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, message) = await service.RegisterPlayerAsync(tournament.Id, "12345");

        Assert.True(success);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://script.google.com/macros/s/test/exec", capturedRequest.RequestUri?.ToString());
        Assert.NotNull(capturedBody);

        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody);
        var root = doc.RootElement;
        Assert.Equal("Friday Deepstack (25.09.2026 19:00)", root.GetProperty("tournamentTitle").GetString());
        Assert.Equal("Иван Иванов", root.GetProperty("playerName").GetString());
        Assert.Equal("1266", root.GetProperty("clubCardId").GetString());
        Assert.Equal("79991234567", root.GetProperty("phoneNumber").GetString());
        Assert.Equal("VK Mini App", root.GetProperty("source").GetString());
        Assert.Contains("PokerClubApp/1.0", capturedRequest.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task RegisterPlayerAsync_WhenWebhookUrlHasWhitespaceAndReturnsRedirect_Succeeds()
    {
        using var context = CreateInMemoryDbContext();
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = new City { Name = "Пермь", Slug = "perm" }, IsActive = true };
        var tournament = new Tournament
        {
            Title = "   ",
            Club = club,
            StartTime = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc),
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 30
        };
        var user = new User
        {
            VkId = "55555",
            FirstName = "Игрок",
            LastName = "VK",
            Nickname = "  LuckyGuy  ",
            ClubCardId = " 999 ",
            PhoneNumber = " +79001234567 "
        };
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new TestHttpMessageHandler(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.Redirect);
            resp.Headers.Location = new Uri("https://script.googleusercontent.com/macros/echo?user_content_key=123");
            return resp;
        });
        var httpClient = new HttpClient(mockHandler);

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheets:RegistrationWebhookUrl"] = "   https://script.google.com/macros/s/redirect/exec \n  "
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, message) = await service.RegisterPlayerAsync(tournament.Id, "55555");

        Assert.True(success);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://script.google.com/macros/s/redirect/exec", capturedRequest.RequestUri?.ToString());
        Assert.NotNull(capturedBody);

        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody);
        var root = doc.RootElement;
        Assert.Equal("Турнир (25.09.2026 05:00)", root.GetProperty("tournamentTitle").GetString());
        Assert.Equal("LuckyGuy", root.GetProperty("playerName").GetString());
        Assert.Equal("999", root.GetProperty("clubCardId").GetString());
        Assert.Equal("79001234567", root.GetProperty("phoneNumber").GetString());
        Assert.Equal("VK Mini App", root.GetProperty("source").GetString());
    }

    [Fact]
    public async Task RegisterPlayerAsync_StripsLeadingPlusFromPhoneNumber_AndFormatsDateInPermTimeUtcPlus5()
    {
        using var context = CreateInMemoryDbContext();
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = new City { Name = "Пермь", Slug = "perm" }, IsActive = true };
        var startTime = new DateTime(2026, 9, 21, 14, 30, 0, DateTimeKind.Utc); // 14:30 UTC -> 19:30 Perm (UTC+5)
        var tournament = new Tournament
        {
            Title = "Perm Time Test",
            Club = club,
            StartTime = startTime,
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 30
        };
        var user = new User
        {
            VkId = "777888",
            FirstName = "Алексей",
            LastName = "Смирнов",
            ClubCardId = "2048",
            PhoneNumber = "+7 (999) 000-11-22"
        };
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new TestHttpMessageHandler(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(mockHandler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheets:RegistrationWebhookUrl"] = "https://script.google.com/macros/s/perm-test/exec"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, _) = await service.RegisterPlayerAsync(tournament.Id, "777888");

        Assert.True(success);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedBody);

        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody);
        var root = doc.RootElement;
        // 14:30 UTC + 5h = 19:30 Perm time
        Assert.Equal("Perm Time Test (21.09.2026 19:30)", root.GetProperty("tournamentTitle").GetString());
        // Leading plus stripped
        Assert.Equal("7 (999) 000-11-22", root.GetProperty("phoneNumber").GetString());
        Assert.False(root.GetProperty("phoneNumber").GetString()!.StartsWith('+'));
    }

    [Fact]
    public async Task RegisterPlayerAsync_StripsLeadingPlusAndSpaces_WhenPhoneHasWhitespaceOrMultiplePluses()
    {
        using var context = CreateInMemoryDbContext();
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = new City { Name = "Пермь", Slug = "perm" }, IsActive = true };
        var startTime = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc); // 10:00 UTC -> 15:00 Perm
        var tournament = new Tournament
        {
            Title = "Edge Case Phone Test",
            Club = club,
            StartTime = startTime,
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 30
        };
        var user = new User
        {
            VkId = "999111",
            FirstName = "Дмитрий",
            LastName = "Волков",
            ClubCardId = "555",
            PhoneNumber = "  + 79001112233  "
        };
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        string? capturedBody = null;
        var mockHandler = new TestHttpMessageHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(mockHandler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheets:RegistrationWebhookUrl"] = "https://script.google.com/macros/s/edge-phone/exec"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);
        var (success, _) = await service.RegisterPlayerAsync(tournament.Id, "999111");

        Assert.True(success);
        Assert.NotNull(capturedBody);

        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody);
        var root = doc.RootElement;
        Assert.Equal("Edge Case Phone Test (21.09.2026 15:00)", root.GetProperty("tournamentTitle").GetString());
        Assert.Equal("79001112233", root.GetProperty("phoneNumber").GetString());
        Assert.False(root.GetProperty("phoneNumber").GetString()!.StartsWith('+'));
    }

    [Fact]
    public async Task RegisterPlayerAsync_WhenWebhookFails_StillSuccessfullyRegistersPlayer()
    {
        using var context = CreateInMemoryDbContext();
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = new City { Name = "Пермь", Slug = "perm" }, IsActive = true };
        var tournament = new Tournament
        {
            Title = "Saturday Bounty",
            Club = club,
            StartTime = DateTime.UtcNow.AddDays(1),
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 30
        };
        var user = new User
        {
            VkId = "99999",
            FirstName = "Петр",
            LastName = "Петров"
        };
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var mockHandler = new TestHttpMessageHandler(req =>
        {
            throw new HttpRequestException("Network failure to Google Sheets");
        });
        var httpClient = new HttpClient(mockHandler);

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheets:RegistrationWebhookUrl"] = "https://script.google.com/macros/s/failing/exec"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, message) = await service.RegisterPlayerAsync(tournament.Id, "99999");

        // Registration MUST succeed despite webhook failure
        Assert.True(success);
        Assert.Contains("Вы успешно записаны на турнир", message);

        var reg = await context.Registrations.FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        Assert.NotNull(reg);
        Assert.Equal(RegStatus.Active, reg.Status);
    }

    [Fact]
    public async Task RegisterPlayerAsync_WhenReactivatingCanceledRegistration_SendsWebhook()
    {
        using var context = CreateInMemoryDbContext();
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = new City { Name = "Пермь", Slug = "perm" }, IsActive = true };
        var tournament = new Tournament
        {
            Title = "Sunday Cup",
            Club = club,
            StartTime = new DateTime(2026, 9, 27, 13, 0, 0, DateTimeKind.Utc),
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 30
        };
        var user = new User
        {
            VkId = "88888",
            FirstName = "Сергей",
            LastName = "Сергеев",
            ClubCardId = "777"
        };
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.Registrations.Add(new Registration
        {
            TournamentId = tournament.Id,
            UserId = user.Id,
            Status = RegStatus.Canceled
        });
        await context.SaveChangesAsync();

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockHandler = new TestHttpMessageHandler(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(mockHandler);

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSheets:RegistrationWebhookUrl"] = "https://script.google.com/macros/s/test/exec"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, message) = await service.RegisterPlayerAsync(tournament.Id, "88888");

        Assert.True(success);
        Assert.Contains("Запись успешно восстановлена", message);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedBody);

        using var doc = System.Text.Json.JsonDocument.Parse(capturedBody);
        var root = doc.RootElement;
        Assert.Equal("Sunday Cup (27.09.2026 18:00)", root.GetProperty("tournamentTitle").GetString());
        Assert.Equal("Сергей Сергеев", root.GetProperty("playerName").GetString());
        Assert.Equal("777", root.GetProperty("clubCardId").GetString());
    }

    [Fact]
    public async Task RegisterPlayerAsync_WithCommunityToken_SendsVkConfirmationMessage()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская улица, 59", City = city, IsActive = true };
        var tournament = new Tournament
        {
            Title = "Friday Deepstack",
            StartTime = new DateTime(2026, 9, 25, 19, 0, 0, DateTimeKind.Utc),
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 10,
            Club = club
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync();

        HttpRequestMessage? vkRequest = null;
        string? vkBody = null;

        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("api.vk.com/method/messages.send") == true)
            {
                vkRequest = req;
                vkBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"response\": 12345}")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VkOptions:CommunityToken"] = "test_community_token_123"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, message) = await service.RegisterPlayerAsync(
            tournament.Id,
            "123456789",
            "Иван",
            "Иванов"
        );

        Assert.True(success);
        Assert.NotNull(vkRequest);
        Assert.NotNull(vkBody);
        Assert.Contains("user_id=123456789", vkBody);
        Assert.Contains("peer_id=123456789", vkBody);
        Assert.Contains("access_token=test_community_token_123", vkBody);
        Assert.Contains("v=5.199", vkBody);
        Assert.Contains("random_id=", vkBody);
        Assert.Contains("Friday+Deepstack", vkBody);
    }

    [Fact]
    public async Task RegisterPlayerAsync_WhenVkFails_RegistrationStillSucceeds()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tournament = new Tournament
        {
            Title = "Saturday Bounty",
            StartTime = new DateTime(2026, 9, 26, 18, 0, 0, DateTimeKind.Utc),
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 10,
            Club = club
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync();

        var handler = new TestHttpMessageHandler(req =>
        {
            throw new HttpRequestException("VK Network failure");
        });
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VkOptions:CommunityToken"] = "token"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, message) = await service.RegisterPlayerAsync(tournament.Id, "999888");

        Assert.True(success);
        Assert.Contains("Вы успешно записаны на турнир", message);
    }

    [Fact]
    public async Task UpdateTournamentAsync_UpdatesAllFieldsSuccessfully()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Old Title",
            Format = "NL Holdem",
            BuyIn = 1000,
            MaxSeats = 30,
            StartTime = DateTime.UtcNow.AddDays(1),
            RegistrationEnd = DateTime.UtcNow.AddDays(1).AddHours(-1),
            Description = "Old description",
            Status = TournamentStatus.RegistrationOpen
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var newStartTime = DateTime.SpecifyKind(new DateTime(2026, 10, 5, 20, 0, 0), DateTimeKind.Utc);
        var newRegEnd = DateTime.SpecifyKind(new DateTime(2026, 10, 5, 19, 30, 0), DateTimeKind.Utc);

        var (success, updated, message) = await service.UpdateTournamentAsync(
            id: tour.Id,
            title: "New Championship",
            format: "Omaha PLO",
            buyIn: 5000,
            maxSeats: 60,
            startTime: newStartTime,
            description: "New description",
            registrationEnd: newRegEnd,
            status: TournamentStatus.Announced
        );

        Assert.True(success);
        Assert.NotNull(updated);
        Assert.Equal("New Championship", updated.Title);
        Assert.Equal("Omaha PLO", updated.Format);
        Assert.Equal(5000, updated.BuyIn);
        Assert.Equal(60, updated.MaxSeats);
        Assert.Equal(newStartTime, updated.StartTime);
        Assert.Equal(newRegEnd, updated.RegistrationEnd);
        Assert.Equal("New description", updated.Description);
        Assert.Equal(TournamentStatus.Announced, updated.Status);

        var reloaded = await context.Tournaments.FindAsync(tour.Id);
        Assert.Equal("New Championship", reloaded?.Title);
        Assert.Equal(5000, reloaded?.BuyIn);
    }

    [Fact]
    public async Task UpdateTournamentAsync_ClearsRegistrationEnd_WhenFlagIsTrue()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Tour With RegEnd",
            StartTime = DateTime.UtcNow.AddDays(1),
            RegistrationEnd = DateTime.UtcNow.AddDays(1).AddHours(-1),
            MaxSeats = 30
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        var (success, updated, message) = await service.UpdateTournamentAsync(
            id: tour.Id,
            clearRegistrationEnd: true
        );

        Assert.True(success);
        Assert.NotNull(updated);
        Assert.Null(updated.RegistrationEnd);

        var reloaded = await context.Tournaments.FindAsync(tour.Id);
        Assert.Null(reloaded?.RegistrationEnd);
    }

    [Fact]
    public async Task UpdateTournamentAsync_WhenTournamentNotFound_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TournamentService(context);

        var (success, updated, message) = await service.UpdateTournamentAsync(
            id: 99999,
            title: "Non-existent Tour"
        );

        Assert.False(success);
        Assert.Null(updated);
        Assert.Equal("Турнир не найден.", message);
    }

    [Fact]
    public async Task UpdateTournamentAsync_ValidationFailures_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Valid Tournament",
            StartTime = DateTime.UtcNow.AddDays(1),
            MaxSeats = 30,
            BuyIn = 1000
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        // 1. Слишком короткий заголовок
        var (s1, _, m1) = await service.UpdateTournamentAsync(tour.Id, title: "ab");
        Assert.False(s1);
        Assert.Contains("не менее 3 символов", m1);

        // 2. Отрицательный бай-ин
        var (s2, _, m2) = await service.UpdateTournamentAsync(tour.Id, buyIn: -500);
        Assert.False(s2);
        Assert.Contains("Бай-ин не может быть отрицательным", m2);

        // 3. Некорректное количество мест
        var (s3, _, m3) = await service.UpdateTournamentAsync(tour.Id, maxSeats: 0);
        Assert.False(s3);
        Assert.Contains("Количество мест должно быть", m3);

        // 4. Некорректный год
        var (s4, _, m4) = await service.UpdateTournamentAsync(tour.Id, startTime: new DateTime(1990, 1, 1));
        Assert.False(s4);
        Assert.Contains("корректную дату и время", m4);

        // 5. Дата default
        var (s5, _, m5) = await service.UpdateTournamentAsync(tour.Id, startTime: default(DateTime));
        Assert.False(s5);
        Assert.Contains("корректную дату и время", m5);
    }

    [Fact]
    public async Task UpdateTournamentAsync_WhenMaxSeatsLessThanRegistered_ReturnsFalse()
    {
        using var context = CreateInMemoryDbContext();
        var user1 = new User { VkId = "u1", FirstName = "User1", CreatedAt = DateTime.UtcNow };
        var user2 = new User { VkId = "u2", FirstName = "User2", CreatedAt = DateTime.UtcNow };
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Tour With Players",
            StartTime = DateTime.UtcNow.AddDays(1),
            MaxSeats = 10,
            Status = TournamentStatus.RegistrationOpen
        };
        tour.Registrations.Add(new Registration { User = user1, Status = RegStatus.Active });
        tour.Registrations.Add(new Registration { User = user2, Status = RegStatus.Active });

        context.Users.AddRange(user1, user2);
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        var (success, _, message) = await service.UpdateTournamentAsync(tour.Id, maxSeats: 1);
        Assert.False(success);
        Assert.Contains("не может быть меньше числа уже зарегистрированных игроков", message);
    }

    [Fact]
    public async Task UpdateTournamentAsync_WhenAddressMatchesExistingClub_SwitchesClubWithoutMutatingOriginal()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club1 = new Club { Name = "Club 1", Address = "Адрес 1", City = city, IsActive = true };
        var club2 = new Club { Name = "Club 2", Address = "Адрес 2", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club1,
            Title = "Tour Address Test",
            StartTime = DateTime.UtcNow.AddDays(1),
            MaxSeats = 30
        };
        context.Cities.Add(city);
        context.Clubs.AddRange(club1, club2);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        var (success, updated, _) = await service.UpdateTournamentAsync(tour.Id, address: "Адрес 2");
        Assert.True(success);
        Assert.NotNull(updated?.Club);
        Assert.Equal(club2.Id, updated.ClubId);
        Assert.Equal("Адрес 1", club1.Address);
    }

    [Fact]
    public async Task UpdateTournamentAsync_UpdatesClubAddress_WhenAddressProvided()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Старый адрес 1", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Tour Address Test",
            StartTime = DateTime.UtcNow.AddDays(1),
            MaxSeats = 30
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);

        var (success, updated, _) = await service.UpdateTournamentAsync(tour.Id, address: "Новый адрес 100");
        Assert.True(success);
        Assert.NotNull(updated?.Club);
        Assert.Equal("Новый адрес 100", updated.Club.Address);
    }

    [Fact]
    public async Task TournamentsController_UpdateTournament_ValidatesFields_ReturnsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament { Club = club, Title = "Tour Test", StartTime = DateTime.UtcNow.AddDays(1), MaxSeats = 30 };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        // 1. Пустое название
        var res1 = await controller.UpdateTournament(tour.Id, new UpdateTournamentRequest(Title: "  "));
        Assert.IsType<BadRequestObjectResult>(res1.Result);

        // 2. Мест 0
        var res2 = await controller.UpdateTournament(tour.Id, new UpdateTournamentRequest(MaxSeats: 0));
        Assert.IsType<BadRequestObjectResult>(res2.Result);

        // 3. Отрицательный бай-ин
        var res3 = await controller.UpdateTournament(tour.Id, new UpdateTournamentRequest(BuyIn: -100));
        Assert.IsType<BadRequestObjectResult>(res3.Result);

        // 4. Дата по умолчанию
        var res4 = await controller.UpdateTournament(tour.Id, new UpdateTournamentRequest(StartTime: default(DateTime)));
        Assert.IsType<BadRequestObjectResult>(res4.Result);
    }

    [Fact]
    public async Task TournamentsController_UpdateTournament_WhenNotFound_ReturnsNotFound()
    {
        using var context = CreateInMemoryDbContext();
        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        var res = await controller.UpdateTournament(88888, new UpdateTournamentRequest(Title: "Valid Title"));
        Assert.IsType<NotFoundObjectResult>(res.Result);
    }

    [Fact]
    public async Task TournamentsController_UpdateTournament_WhenValid_ReturnsOkWithDetailDto()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Tour Before Edit",
            StartTime = DateTime.UtcNow.AddDays(2),
            MaxSeats = 30,
            BuyIn = 1500,
            Format = "NL Holdem",
            Description = "Before edit"
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        var newTime = DateTime.UtcNow.AddDays(3);
        var request = new UpdateTournamentRequest(
            Title: "Tour After Edit",
            BuyIn: 3000,
            MaxSeats: 45,
            StartTime: newTime,
            Description: "After edit"
        );

        var res = await controller.UpdateTournament(tour.Id, request);
        var okResult = Assert.IsType<OkObjectResult>(res.Result);
        var detail = Assert.IsType<TournamentDetailDto>(okResult.Value);

        Assert.Equal("Tour After Edit", detail.Title);
        Assert.Equal(3000, detail.BuyIn);
        Assert.Equal(45, detail.MaxSeats);
        Assert.Equal("After edit", detail.Description);
    }

    [Fact]
    public async Task TournamentsController_GetSchedule_PopulatesClubAddress()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Schedule Tour",
            StartTime = DateTime.UtcNow.AddDays(1),
            MaxSeats = 30,
            Status = TournamentStatus.RegistrationOpen
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        var res = await controller.GetSchedule(cityId: null, clubId: null, includeFinished: false);
        var okResult = Assert.IsType<OkObjectResult>(res.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);

        var first = Assert.Single(list);
        Assert.Equal("Монастырская 59", first.ClubAddress);
    }

    [Fact]
    public async Task RegisterPlayerAsync_WithIdPrefixedVkId_StripsPrefixAndSendsVkConfirmationMessage()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская улица, 59", City = city, IsActive = true };
        var tournament = new Tournament
        {
            Title = "Friday Deepstack",
            StartTime = new DateTime(2026, 9, 25, 19, 0, 0, DateTimeKind.Utc),
            Status = TournamentStatus.RegistrationOpen,
            MaxSeats = 10,
            Club = club
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync();

        HttpRequestMessage? vkRequest = null;
        string? vkBody = null;

        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("api.vk.com/method/messages.send") == true)
            {
                vkRequest = req;
                vkBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"response\": 12345}")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VkOptions:CommunityToken"] = "test_community_token_123"
            })
            .Build();

        var service = new TournamentService(context, config, null, httpClient);

        var (success, message) = await service.RegisterPlayerAsync(
            tournament.Id,
            "id308885723",
            "Иван",
            "Иванов"
        );

        Assert.True(success);
        Assert.NotNull(vkRequest);
        Assert.NotNull(vkBody);
        Assert.Contains("user_id=308885723", vkBody);
        Assert.Contains("peer_id=308885723", vkBody);
        Assert.Contains("access_token=test_community_token_123", vkBody);
    }

    [Fact]
    public async Task AdminController_TestVkMessage_SendsMessageAndReturnsResponse()
    {
        HttpRequestMessage? vkRequest = null;
        string? vkBody = null;

        var handler = new TestHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("api.vk.com/method/messages.send") == true)
            {
                vkRequest = req;
                vkBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"response\": 777}")
                };
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VkOptions:CommunityToken"] = "test_token_xyz"
            })
            .Build();

        var factory = new TestHttpClientFactory(httpClient);
        var controller = new AdminController(
            null!,
            null,
            null,
            config,
            null,
            factory);

        var result = await controller.TestVkMessage("id308885723", "Тестовое сообщение");
        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("Success").GetBoolean());
        Assert.Equal(308885723L, doc.RootElement.GetProperty("TargetVkId").GetInt64());
        Assert.NotNull(vkRequest);
        Assert.NotNull(vkBody);
        Assert.Contains("user_id=308885723", vkBody);
        Assert.Contains("access_token=test_token_xyz", vkBody);
    }

    [Fact]
    public async Task CreateTournament_WithStartingStack_SetsStartingStackCorrectly()
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
            clubId: club.Id,
            title: "Deepstack 20K",
            format: "NL Holdem",
            buyIn: 2000,
            maxSeats: 30,
            startTime: startTime,
            description: "20k chips",
            startingStack: 20000
        );

        Assert.True(success);
        Assert.NotNull(tournament);
        Assert.Equal(20000, tournament.StartingStack);
    }

    [Fact]
    public async Task TournamentsController_UpdateTournament_WithStartingStack_PersistsAndReturnsInDtos()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tour = new Tournament
        {
            Club = club,
            Title = "Tour Stack Test",
            StartTime = DateTime.UtcNow.AddDays(2),
            MaxSeats = 30,
            BuyIn = 1500,
            Format = "NL Holdem",
            StartingStack = 10000
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        var updateReq = new UpdateTournamentRequest(
            StartingStack: 15000
        );

        var updateRes = await controller.UpdateTournament(tour.Id, updateReq);
        var okDetail = Assert.IsType<OkObjectResult>(updateRes.Result);
        var detail = Assert.IsType<TournamentDetailDto>(okDetail.Value);

        Assert.Equal(15000, detail.StartingStack);
        Assert.Equal(15000, detail.StartingChips);

        var scheduleRes = await controller.GetSchedule(null, null, false);
        var okSchedule = Assert.IsType<OkObjectResult>(scheduleRes.Result);
        var scheduleList = Assert.IsType<List<TournamentScheduleDto>>(okSchedule.Value);
        var scheduleItem = Assert.Single(scheduleList);

        Assert.Equal(15000, scheduleItem.StartingStack);
        Assert.Equal(15000, scheduleItem.StartingChips);

        var badUpdate = await controller.UpdateTournament(tour.Id, new UpdateTournamentRequest(StartingStack: 0));
        Assert.IsType<BadRequestObjectResult>(badUpdate.Result);

        var badCreate = await controller.CreateTournament(new CreateTournamentRequest(
            Title: "Valid Title",
            ClubId: club.Id,
            StartTime: DateTime.UtcNow.AddDays(1),
            MaxSeats: 30,
            StartingStack: -500
        ));
        Assert.IsType<BadRequestObjectResult>(badCreate.Result);
    }

    [Fact]
    public async Task GetSchedule_IncludesRunningTournamentsInActiveSchedule()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", City = city, IsActive = true };
        var runningTour = new Tournament
        {
            Club = club,
            Title = "Running Live Event",
            Status = TournamentStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            MaxSeats = 30
        };
        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(runningTour);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var schedule = await service.GetScheduleAsync(cityId: null, clubId: null, includeFinished: false);

        var single = Assert.Single(schedule);
        Assert.Equal("Running Live Event", single.Title);
        Assert.Equal(TournamentStatus.Running, single.Status);
    }

    [Fact]
    public async Task TournamentsController_GetSchedule_WhenSelectedCityEmpty_FallbacksToAllCitiesSchedule()
    {
        using var context = CreateInMemoryDbContext();
        var cityPerm = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var cityEkb = new City { Name = "Екатеринбург", Slug = "ekb", IsActive = true };
        var clubEkb = new Club { Name = "Ekb Club", City = cityEkb, IsActive = true };

        var tourEkb = new Tournament
        {
            Club = clubEkb,
            Title = "Ekb Championship",
            Status = TournamentStatus.RegistrationOpen,
            StartTime = DateTime.UtcNow.AddDays(2),
            MaxSeats = 40
        };

        context.Cities.AddRange(cityPerm, cityEkb);
        context.Clubs.Add(clubEkb);
        context.Tournaments.Add(tourEkb);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        // Пользователь запрашивает Пермь (где турниров нет), но в системе есть турнир в Екб
        var res = await controller.GetSchedule(cityId: cityPerm.Id, clubId: null, includeFinished: false);
        var okResult = Assert.IsType<OkObjectResult>(res.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);

        var first = Assert.Single(list);
        Assert.Equal("Ekb Championship", first.Title);
    }

    [Fact]
    public async Task TournamentsController_GetSchedule_WhenNoActiveTournaments_FallbacksToFinishedTournaments()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", City = city, IsActive = true };

        var tourFinished = new Tournament
        {
            Club = club,
            Title = "Finished Last Week Cup",
            Status = TournamentStatus.Finished,
            StartTime = DateTime.UtcNow.AddDays(-2),
            MaxSeats = 30
        };

        context.Cities.Add(city);
        context.Clubs.Add(club);
        context.Tournaments.Add(tourFinished);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        // Запрашиваем без includeFinished: активных нет, но отдаются завершенные
        var res = await controller.GetSchedule(cityId: null, clubId: null, includeFinished: false);
        var okResult = Assert.IsType<OkObjectResult>(res.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);

        var first = Assert.Single(list);
        Assert.Equal("Finished Last Week Cup", first.Title);
        Assert.Equal(TournamentStatus.Finished, first.Status);
    }

    [Fact]
    public async Task TournamentsController_GetSchedule_WhenSpecificCityAndClubRequested_FallbacksToOtherActiveTournaments()
    {
        using var context = CreateInMemoryDbContext();
        var cityPerm = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var cityEkb = new City { Name = "Екатеринбург", Slug = "ekb", IsActive = true };
        var clubPerm = new Club { Name = "Monte Carlo Perm", City = cityPerm, IsActive = true };
        var clubEkb = new Club { Name = "Monte Carlo Ekb", City = cityEkb, IsActive = true };

        var tourEkb = new Tournament
        {
            Club = clubEkb,
            Title = "Ekb Grand Cup",
            Status = TournamentStatus.RegistrationOpen,
            StartTime = DateTime.UtcNow.AddDays(3),
            MaxSeats = 50
        };

        context.Cities.AddRange(cityPerm, cityEkb);
        context.Clubs.AddRange(clubPerm, clubEkb);
        context.Tournaments.Add(tourEkb);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        // Пользователь запрашивает конкретный клуб в Перми (где турниров нет), но в системе есть турнир в Екб
        var res = await controller.GetSchedule(cityId: cityPerm.Id, clubId: clubPerm.Id, includeFinished: false);
        var okResult = Assert.IsType<OkObjectResult>(res.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);

        var first = Assert.Single(list);
        Assert.Equal("Ekb Grand Cup", first.Title);
    }

    [Fact]
    public async Task TournamentsController_GetSchedule_WhenClubRequestedInSameCity_FallbacksToOtherClubInSameCity()
    {
        using var context = CreateInMemoryDbContext();
        var cityPerm = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var clubPerm1 = new Club { Name = "Monte Carlo Club 1", City = cityPerm, IsActive = true };
        var clubPerm2 = new Club { Name = "Monte Carlo Club 2", City = cityPerm, IsActive = true };

        var tourClub2 = new Tournament
        {
            Club = clubPerm2,
            Title = "Club 2 Open",
            Status = TournamentStatus.RegistrationOpen,
            StartTime = DateTime.UtcNow.AddDays(1),
            MaxSeats = 30
        };

        context.Cities.Add(cityPerm);
        context.Clubs.AddRange(clubPerm1, clubPerm2);
        context.Tournaments.Add(tourClub2);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var controller = new TournamentsController(service);

        // Пользователь запрашивает клуб 1 в Перми (турниров нет), fallback находит турнир в клубе 2 в Перми
        var res = await controller.GetSchedule(cityId: cityPerm.Id, clubId: clubPerm1.Id, includeFinished: false);
        var okResult = Assert.IsType<OkObjectResult>(res.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);

        var first = Assert.Single(list);
        Assert.Equal("Club 2 Open", first.Title);
    }

    [Fact]
    public void TournamentDtos_StartingChips_FallbackToDefaultWhenZeroOrNegative()
    {
        var scheduleZero = new TournamentScheduleDto(
            1, "Test", "NL", 1000, null, 20, DateTime.UtcNow, TournamentStatus.Announced, 1, "Club", "Perm", 0, false, null, null, 0);
        Assert.Equal(10000, scheduleZero.StartingChips);

        var scheduleNegative = new TournamentScheduleDto(
            1, "Test", "NL", 1000, null, 20, DateTime.UtcNow, TournamentStatus.Announced, 1, "Club", "Perm", 0, false, null, null, -500);
        Assert.Equal(10000, scheduleNegative.StartingChips);

        var schedulePositive = new TournamentScheduleDto(
            1, "Test", "NL", 1000, null, 20, DateTime.UtcNow, TournamentStatus.Announced, 1, "Club", "Perm", 0, false, null, null, 25000);
        Assert.Equal(25000, schedulePositive.StartingChips);

        var detailZero = new TournamentDetailDto(
            1, "Test", "NL", 1000, null, 20, DateTime.UtcNow, TournamentStatus.Announced, 1, "Club", "Perm", "Address", 0, false, new List<RegisteredPlayerDto>(), null, 0);
        Assert.Equal(10000, detailZero.StartingChips);

        var detailPositive = new TournamentDetailDto(
            1, "Test", "NL", 1000, null, 20, DateTime.UtcNow, TournamentStatus.Announced, 1, "Club", "Perm", "Address", 0, false, new List<RegisteredPlayerDto>(), null, 30000);
        Assert.Equal(30000, detailPositive.StartingChips);
    }

    [Fact]
    public async Task TournamentsController_GetSchedule_WhenServiceThrows_ReturnsEmptyListFallback()
    {
        var throwingService = new ThrowingTournamentService();
        var controller = new TournamentsController(throwingService);
        var result = await controller.GetSchedule(1, 1, false);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);
        Assert.Empty(list);
    }

    private class ThrowingTournamentService : ITournamentService
    {
        public Task<List<Tournament>> GetScheduleAsync(int? cityId, int? clubId, bool includeFinished = false)
            => throw new InvalidOperationException("Simulated database failure");
        public Task<Tournament?> GetTournamentByIdAsync(int id)
            => throw new InvalidOperationException("Simulated database failure");
        public Task<List<Tournament>> GetUserTournamentsAsync(string vkId)
            => throw new InvalidOperationException("Simulated database failure");
        public Task<(bool Success, string Message)> RegisterPlayerAsync(int tournamentId, string vkId, string? firstName = null, string? lastName = null, string? avatarUrl = null)
            => throw new InvalidOperationException("Simulated database failure");
        public Task<(bool Success, string Message)> CancelRegistrationAsync(int tournamentId, string vkId)
            => throw new InvalidOperationException("Simulated database failure");
        public Task<(bool Success, string Message)> DeleteTournamentAsync(int id)
            => throw new InvalidOperationException("Simulated database failure");
        public Task<(bool Success, Tournament? Tournament, string Message)> CreateTournamentAsync(int? clubId, string title, string? format, decimal buyIn, int maxSeats, DateTime startTime, string? description, int? cityId = null, string? address = null, DateTime? registrationEnd = null, int startingStack = 10000)
            => throw new InvalidOperationException("Simulated database failure");
        public Task<(bool Success, Tournament? Tournament, string Message)> UpdateTournamentAsync(int id, int? clubId = null, string? title = null, string? format = null, decimal? buyIn = null, int? maxSeats = null, DateTime? startTime = null, string? description = null, int? cityId = null, string? address = null, DateTime? registrationEnd = null, TournamentStatus? status = null, bool clearRegistrationEnd = false, int? startingStack = null)
            => throw new InvalidOperationException("Simulated database failure");
    }

    [Fact]
    public async Task TournamentsController_GetTournament_WhenServiceThrows_ReturnsNotFoundFallback()
    {
        var throwingService = new ThrowingTournamentService();
        var controller = new TournamentsController(throwingService);
        var result = await controller.GetTournament(999);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task TournamentsController_GetMyTournaments_WhenServiceThrows_ReturnsEmptyListFallback()
    {
        var throwingService = new ThrowingTournamentService();
        var controller = new TournamentsController(throwingService);
        var httpContext = new DefaultHttpContext();
        httpContext.Items["VkUserId"] = "12345";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetMyTournaments();
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<TournamentScheduleDto>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task TournamentsController_Mutations_WhenServiceThrows_ReturnsBadRequestFallback()
    {
        var throwingService = new ThrowingTournamentService();
        var controller = new TournamentsController(throwingService);
        var httpContext = new DefaultHttpContext();
        httpContext.Items["VkUserId"] = "12345";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var regResult = await controller.Register(new RegisterPlayerRequest(1, "12345"));
        Assert.IsType<BadRequestObjectResult>(regResult);

        var unregResult = await controller.Unregister(new CancelRegistrationRequest(1, "12345"));
        Assert.IsType<BadRequestObjectResult>(unregResult);

        var createResult = await controller.CreateTournament(new CreateTournamentRequest("New Tournament", ClubId: 1, Format: "NL", BuyIn: 1000, MaxSeats: 20, StartTime: DateTime.UtcNow.AddDays(1)));
        Assert.IsType<BadRequestObjectResult>(createResult.Result);

        var updateResult = await controller.UpdateTournament(1, new UpdateTournamentRequest("Updated"));
        Assert.IsType<BadRequestObjectResult>(updateResult.Result);

        var deleteResult = await controller.DeleteTournament(1);
        Assert.IsType<BadRequestObjectResult>(deleteResult);
    }

    [Fact]
    public async Task TournamentService_QuerySanitization_NormalizesZeroStartingStackToDefault()
    {
        using var context = CreateInMemoryDbContext();
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        context.Cities.Add(city);
        context.Clubs.Add(club);

        var tourZero = new Tournament
        {
            Club = club,
            Title = "Zero Stack Tournament",
            BuyIn = 1000,
            MaxSeats = 20,
            StartTime = DateTime.UtcNow.AddHours(2),
            Status = TournamentStatus.Announced,
            StartingStack = 0
        };
        context.Tournaments.Add(tourZero);
        await context.SaveChangesAsync();

        var service = new TournamentService(context);
        var schedule = await service.GetScheduleAsync(city.Id, club.Id, false);
        var item = Assert.Single(schedule);
        Assert.Equal(10000, item.StartingStack);

        var detail = await service.GetTournamentByIdAsync(tourZero.Id);
        Assert.NotNull(detail);
        Assert.Equal(10000, detail.StartingStack);
    }

    [Fact]
    public async Task DbInitializer_EnsureDatabaseSchemaAsync_ExecutesWithoutError()
    {
        using var context = CreateInMemoryDbContext();
        await DbInitializer.EnsureDatabaseSchemaAsync(context);
        await DbInitializer.CleanFakeTournamentsAsync(context);
        Assert.True(true);
    }

    [Fact]
    public void BuildRegistrationConfirmationMessage_WithDefaultMonteCarloClub_FormatsExactTemplate()
    {
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "ул. Куйбышева, 7, кафе «Гости»", City = city, IsActive = true };
        // 25 сентября 2026 14:00 UTC = 19:00 в Перми (UTC+5)
        var utcStartTime = new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc);
        var tournament = new Tournament
        {
            Club = club,
            Title = "🏆💣 GRAND OPENING BOMB POT",
            BuyIn = 2000,
            MaxSeats = 30,
            StartTime = utcStartTime,
            Status = TournamentStatus.RegistrationOpen
        };

        var message = TournamentService.BuildRegistrationConfirmationMessage(tournament);

        Assert.Contains("♠️ Вы успешно зарегистрированы на турнир «🏆💣 GRAND OPENING BOMB POT»!", message);
        Assert.Contains("📅 Дата: 25.09", message);
        Assert.Contains("🕗 Начало турнира: 19:00", message);
        Assert.Contains("🕕 Сбор гостей и регистрация на турнир: с 18:30", message);
        Assert.Contains("📍 Адрес: ул. Куйбышева, 7, кафе «Гости».\nВход с улицы Монастырской — в первую арку.", message);
        Assert.Contains("🍽 Меню клуба:\nhttps://vk.ru/wall-238367404_5", message);
        Assert.Contains("💨 Кальян для игроков — всего 300 ₽.", message);
        Assert.Contains("💵 Обратите внимание: вся оплата в клубе производится только наличными.", message);
        Assert.Contains("Ждём вас за столом Monte Carlo! ♠️", message);
    }

    [Fact]
    public void BuildRegistrationConfirmationMessage_WithCustomClubAddress_OutputsCustomAddress()
    {
        var city = new City { Name = "Екатеринбург", Slug = "ekb", IsActive = true };
        var club = new Club { Name = "Ekb Spot", Address = "ул. Малышева, 44", City = city, IsActive = true };
        var utcStartTime = new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc);
        var tournament = new Tournament
        {
            Club = club,
            Title = "Weekend Cup",
            BuyIn = 5000,
            MaxSeats = 40,
            StartTime = utcStartTime,
            Status = TournamentStatus.RegistrationOpen
        };

        var message = TournamentService.BuildRegistrationConfirmationMessage(tournament);

        Assert.Contains("♠️ Вы успешно зарегистрированы на турнир «Weekend Cup»!", message);
        Assert.Contains("📍 Адрес: ул. Малышева, 44.", message);
    }

    [Fact]
    public void BuildRegistrationConfirmationMessage_WithQuotedTitle_CleansQuotes()
    {
        var city = new City { Name = "Пермь", Slug = "perm", IsActive = true };
        var club = new Club { Name = "Monte Carlo", Address = "Монастырская 59", City = city, IsActive = true };
        var tournament = new Tournament
        {
            Club = club,
            Title = "«Bounty Hunter»",
            StartTime = new DateTime(2026, 9, 25, 14, 0, 0, DateTimeKind.Utc),
            Status = TournamentStatus.RegistrationOpen
        };

        var message = TournamentService.BuildRegistrationConfirmationMessage(tournament);

        Assert.Contains("♠️ Вы успешно зарегистрированы на турнир «Bounty Hunter»!", message);
        Assert.DoesNotContain("««", message);
        Assert.DoesNotContain("»»", message);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public TestHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}


