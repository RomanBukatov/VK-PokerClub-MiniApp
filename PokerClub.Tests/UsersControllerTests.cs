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
using PokerClub.Infrastructure.Data;
using Xunit;

namespace PokerClub.Tests;

public class UsersControllerTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetMe_WhenUserDoesNotExist_CreatesAndReturnsUserProfile()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "999";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("999", profile.VkId);
        Assert.Equal("Новичок", profile.Status);
        Assert.Equal(0, profile.TotalRating);

        var dbUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "999");
        Assert.NotNull(dbUser);
    }

    [Fact]
    public async Task UpdateProfile_ValidNicknameAndData_UpdatesUser()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "888",
            FirstName = "Иван",
            LastName = "Иванов",
            TotalRating = 250
        };
        context.Users.Add(user);
        context.Users.Add(new User { VkId = "sheet_card_123", ClubCardId = "123" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "888";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            Nickname: "Pro_Ivan",
            PhoneNumber: "+7 (999) 000-11-22",
            ClubCardId: "123",
            AcceptedTerms: true
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("Pro_Ivan", profile.Nickname);
        Assert.Equal("+7 (999) 000-11-22", profile.PhoneNumber);
        Assert.Equal("123", profile.ClubCardId);
        Assert.NotNull(profile.AcceptedTermsAt);
        Assert.Equal("Новичок", profile.Status); // 250 rating is Новичок (< 300)
    }

    [Fact]
    public async Task UpdateProfile_ShortNickname_ReturnsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "888";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(Nickname: "ab", ClubCardId: "123"); // < 3 chars

        var actionResult = await controller.UpdateProfile(request);
        Assert.IsType<BadRequestObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task UpdateProfile_WithMatchingSheetUser_MigratesStats()
    {
        using var context = CreateInMemoryDbContext();
        var sheetUser = new User
        {
            VkId = "sheet_1_abc12345",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 486,
            TournamentsPlayed = 22,
            WinsCount = 2,
            Top3Count = 5,
            Top10Count = 10,
            KnockoutsCount = 40,
            AvgPlace = 7.36,
            ClubCardId = "1"
        };
        context.Users.Add(sheetUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "real_tg_user_1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Лукашенко Василий",
            Nickname: "Vasia_King",
            PhoneNumber: "+7 (999) 111-22-33",
            ClubCardId: "1"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(486, profile.TotalRating);
        Assert.Equal(22, profile.TournamentsPlayed);
        Assert.Equal(2, profile.WinsCount);
        Assert.Equal("Игрок", profile.Status); // 486 is Игрок (300-599)
        Assert.Equal("1", profile.ClubCardId);

        // Placeholder sheetUser was removed
        var remainingUsers = await context.Users.ToListAsync();
        Assert.Single(remainingUsers);
        Assert.Equal("real_tg_user_1", remainingUsers[0].VkId);
    }

    [Fact]
    public async Task UpdateProfile_WithoutCard_ReturnsBadRequest400()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "newbie_user_1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // 1. Попытка обновить профиль с ClubCardId = null
        var request = new UpdateProfileRequest(
            FullName: "Лукашенко Василий",
            Nickname: "Newbie_Vasya",
            PhoneNumber: "+7 (915) 000-11-22",
            ClubCardId: null
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var message = badRequestResult.Value?.GetType().GetProperty("message")?.GetValue(badRequestResult.Value)?.ToString()
            ?? badRequestResult.Value?.GetType().GetProperty("Message")?.GetValue(badRequestResult.Value)?.ToString();
        Assert.Contains("Клубный ID обязателен для регистрации", message);

        // 2. Попытка обновить профиль с пустым/пробельным ClubCardId
        var whitespaceRequest = new UpdateProfileRequest(
            FullName: "Лукашенко Василий",
            Nickname: "Newbie_Vasya",
            PhoneNumber: "+7 (915) 000-11-22",
            ClubCardId: "   "
        );
        var wsActionResult = await controller.UpdateProfile(whitespaceRequest);
        var wsBadRequestResult = Assert.IsType<BadRequestObjectResult>(wsActionResult.Result);
        var wsMessage = wsBadRequestResult.Value?.GetType().GetProperty("message")?.GetValue(wsBadRequestResult.Value)?.ToString()
            ?? wsBadRequestResult.Value?.GetType().GetProperty("Message")?.GetValue(wsBadRequestResult.Value)?.ToString();
        Assert.Contains("Клубный ID обязателен для регистрации", wsMessage);
    }

    [Fact]
    public async Task UpdateProfile_WhenCardAlreadyBoundToAnotherRealUser_ReturnsBadRequest400()
    {
        using var context = CreateInMemoryDbContext();
        // Реальный пользователь уже привязал карту 777
        var realUserA = new User
        {
            VkId = "real_user_a",
            FirstName = "Алексей",
            ClubCardId = "777",
            TotalRating = 100
        };
        context.Users.Add(realUserA);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "real_user_b";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Другой реальный пользователь пытается привязать ту же карту 777
        var request = new UpdateProfileRequest(
            Nickname: "Player_B",
            PhoneNumber: "+7 (915) 222-33-44",
            ClubCardId: "777"
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.NotNull(badRequest.Value);
        var messageProp = badRequest.Value.GetType().GetProperty("Message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Equal("Эта клубная карта уже привязана к другому профилю. Обратитесь к администратору клуба.", messageProp);
    }

    [Fact]
    public async Task UpdateProfile_WhenCardPhoneDoesNotMatchUserPhone_ReturnsBadRequest400()
    {
        using var context = CreateInMemoryDbContext();
        // В базе карта 888 закреплена за телефоном +7 (915) 123-45-67
        var sheetUser = new User
        {
            VkId = "sheet_888_test",
            FirstName = "Сергей",
            ClubCardId = "888",
            PhoneNumber = "+7 (915) 123-45-67",
            TotalRating = 300
        };
        context.Users.Add(sheetUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "hijacker_user";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Злоумышленник пытается привязать карту 888 со своим чужим телефоном
        var request = new UpdateProfileRequest(
            Nickname: "Hijacker",
            PhoneNumber: "+7 (915) 999-99-99",
            ClubCardId: "888"
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.NotNull(badRequest.Value);
        var messageProp = badRequest.Value.GetType().GetProperty("Message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Equal("Указанный номер телефона не совпадает с телефоном владельца карты в базе клуба. Если это ваша карта — обратитесь к администратору.", messageProp);

        // Очки не украдены, sheetUser сохранен
        var sheetInDb = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_888_test");
        Assert.NotNull(sheetInDb);
    }

    [Fact]
    public async Task UpdateProfile_WhenCardPhoneMatches_TransfersSheetPointsAndStats()
    {
        using var context = CreateInMemoryDbContext();
        // В базе карта 999 закреплена за телефоном 8 (915) 123-45-67 (формат с 8)
        var sheetUser = new User
        {
            VkId = "sheet_999_test",
            FirstName = "Михаил",
            LastName = "Гуляев",
            ClubCardId = "999",
            PhoneNumber = "8 (915) 123-45-67",
            TotalRating = 350,
            SeasonRating = 350,
            TournamentsPlayed = 12,
            WinsCount = 3,
            Top3Count = 5,
            Top10Count = 8,
            KnockoutsCount = 25,
            AvgPlace = 4.5
        };
        context.Users.Add(sheetUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "legit_user";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Законный владелец вводит свой телефон в формате +7 (915) 123-45-67
        var request = new UpdateProfileRequest(
            Nickname: "Misha_G",
            PhoneNumber: "+7 (915) 123-45-67",
            ClubCardId: "999"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(350, profile.TotalRating);
        Assert.Equal(350, profile.SeasonRating);
        Assert.Equal(12, profile.TournamentsPlayed);
        Assert.Equal(3, profile.WinsCount);
        Assert.Equal("999", profile.ClubCardId);
        Assert.Equal("Игрок", profile.Status); // 350 is Игрок (300-599)

        // sheetUser удален из базы во избежание дубликатов
        var sheetInDb = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_999_test");
        Assert.Null(sheetInDb);
    }

    [Fact]
    public async Task AcceptTerms_SetsAcceptedTermsAt()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "555";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.AcceptTerms(null);
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.NotNull(okResult.Value);

        var user = await context.Users.FirstAsync(u => u.VkId == "555");
        Assert.NotNull(user.AcceptedTermsAt);
    }

    [Fact]
    public void VkAuthValidator_WithTelegramHeader_AuthenticatesSuccessfully()
    {
        var options = Options.Create(new VkOptions
        {
            RequireValidation = true,
            ClientSecret = "secret",
            AdminVkIds = new List<string> { "tg_admin_1" }
        });

        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Telegram-Id"] = "tg_user_99";

        var result = validator.Validate(httpContext);

        Assert.True(result.IsValid);
        Assert.Equal("tg_user_99", result.VkUserId);
        Assert.False(result.IsAdmin);
    }

    [Fact]
    public async Task UpdateProfile_SingleWordFullName_DoesNotDuplicateName()
    {
        using var context = CreateInMemoryDbContext();
        context.Users.Add(new User { VkId = "sheet_card_123", ClubCardId = "123" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "single_word_user";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Иван",
            Nickname: "IvanSuper",
            ClubCardId: "123"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("Иван", profile.FirstName);
        Assert.Equal("", profile.LastName);
        Assert.Equal("Иван", profile.FullName);
    }

    [Fact]
    public async Task GetMe_WithTelegramUserHeader_InitializesTelegramUserData()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Telegram-Id"] = "tg_321";
        httpContext.Request.Headers["X-Telegram-User"] = "{\"id\":321,\"first_name\":\"Павел\",\"last_name\":\"Дуров\",\"photo_url\":\"https://t.me/photo.jpg\"}";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("tg_321", profile.VkId);
        Assert.Equal("Павел", profile.FirstName);
        Assert.Equal("Дуров", profile.LastName);
        Assert.Equal("https://t.me/photo.jpg", profile.AvatarUrl);
    }

    [Theory]
    [InlineData(0, "Новичок")]
    [InlineData(50, "Новичок")]
    [InlineData(100, "Новичок")]
    [InlineData(299, "Новичок")]
    [InlineData(300, "Игрок")]
    [InlineData(599, "Игрок")]
    [InlineData(600, "Претендент")]
    [InlineData(800, "Регуляр")]
    [InlineData(1100, "Тактик")]
    [InlineData(1400, "Стратег")]
    [InlineData(1800, "Профи")]
    [InlineData(2100, "Эксперт")]
    [InlineData(2600, "Мастер")]
    [InlineData(3100, "Грандмастер")]
    [InlineData(3600, "Элита")]
    [InlineData(5000, "Легенда")]
    [InlineData(6500, "Чемпион")]
    [InlineData(10000, "Титан")]
    [InlineData(15000, "Икона Монте-Карло")]
    [InlineData(16000, "Икона МК x2")]
    [InlineData(30000, "Икона МК x2")]
    [InlineData(30001, "Икона МК x3")]
    public void CalculateClubStatus_MonteCarlo15RanksAndPrestige_ReturnsCorrectStatus(int rating, string expectedStatus)
    {
        var status = UsersController.CalculateClubStatus(rating);
        Assert.Equal(expectedStatus, status);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("0")]
    [InlineData(null)]
    public async Task GetMe_WhenUserIsInAdminVkIds_ReturnsIsAdminTrue_RegardlessOfXIsAdminHeader(string? headerValue)
    {
        using var context = CreateInMemoryDbContext();
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "308885723", "8831353" }
        });
        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var controller = new UsersController(context, NullLogger<UsersController>.Instance, validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "308885723";
        if (headerValue != null)
        {
            httpContext.Request.Headers["X-Is-Admin"] = headerValue;
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("308885723", profile.VkId);
        Assert.True(profile.IsAdmin);
    }

    [Fact]
    public async Task GetMe_WhenUserIsNotInAdminVkIds_ReturnsIsAdminFalse()
    {
        using var context = CreateInMemoryDbContext();
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "308885723", "8831353" }
        });
        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var controller = new UsersController(context, NullLogger<UsersController>.Instance, validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "999999999";
        httpContext.Request.Headers["X-Is-Admin"] = "true";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("999999999", profile.VkId);
        Assert.False(profile.IsAdmin);
    }

    [Theory]
    [InlineData("308885723", "false")]
    [InlineData("8831353", "false")]
    [InlineData("id308885723", "false")]
    [InlineData("id8831353", "0")]
    [InlineData("308885723", null)]
    public async Task GetMe_WhenUserIsInAdminVkIds_ReturnsIsAdminTrue_ForAnyConfiguredAdmin(string vkId, string? headerValue)
    {
        using var context = CreateInMemoryDbContext();
        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "308885723", "8831353" }
        });
        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var controller = new UsersController(context, NullLogger<UsersController>.Instance, validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = vkId;
        if (headerValue != null)
        {
            httpContext.Request.Headers["X-Is-Admin"] = headerValue;
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetMe();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(vkId, profile.VkId);
        Assert.True(profile.IsAdmin);
    }

    [Fact]
    public async Task UpdateProfile_WhenUserIsInAdminVkIds_ReturnsIsAdminTrue()
    {
        using var context = CreateInMemoryDbContext();
        context.Users.Add(new User { VkId = "sheet_card_admin1", ClubCardId = "ADMIN-1" });
        await context.SaveChangesAsync();

        var options = Options.Create(new VkOptions
        {
            RequireValidation = false,
            AdminVkIds = new List<string> { "308885723", "8831353" }
        });
        var validator = new VkAuthValidator(options, NullLogger<VkAuthValidator>.Instance);
        var controller = new UsersController(context, NullLogger<UsersController>.Instance, validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "308885723";
        httpContext.Request.Headers["X-Is-Admin"] = "false";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.UpdateProfile(new UpdateProfileRequest
        {
            Nickname = "AdminHero",
            ClubCardId = "ADMIN-1",
            AcceptedTerms = true
        });
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("308885723", profile.VkId);
        Assert.True(profile.IsAdmin);
    }

    [Fact]
    public async Task UpdateProfile_WithThreeWordFio_MatchesTwoWordSheetPlayer_Transfers406Points()
    {
        using var context = CreateInMemoryDbContext();

        // 1. Создаем игрока sheet_* из таблицы Google Sheets с 406 очками и именем "Логинов Дмитрий" (без карты)
        var sheetUser = new User
        {
            VkId = "sheet_12_abc123",
            FirstName = "Дмитрий",
            LastName = "Логинов",
            TotalRating = 406,
            SeasonRating = 0,
            TournamentsPlayed = 19,
            WinsCount = 1,
            Top3Count = 4,
            Top10Count = 10,
            KnockoutsCount = 27,
            AvgPlace = 6.0,
            ClubCardId = null,
            PhoneNumber = null
        };
        context.Users.Add(sheetUser);

        // 2. Создаем реального пользователя VK (например, с 0 очков)
        var realUser = new User
        {
            VkId = "vk_loginov_dmitry",
            FirstName = "Дмитрий",
            LastName = "Логинов",
            TotalRating = 0
        };
        context.Users.Add(realUser);
        context.Users.Add(new User { VkId = "sheet_card_1518", ClubCardId = "1518" });
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_loginov_dmitry";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // 3. Пользователь вводит трехсловное ФИО "Логинов Дмитрий Васильевич" и номер карты "1518"
        var request = new UpdateProfileRequest(
            FullName: "Логинов Дмитрий Васильевич",
            Nickname: "DimaLoginov",
            PhoneNumber: "+7 (999) 111-22-33",
            ClubCardId: "1518",
            AcceptedTerms: true
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        // Проверяем правильный парсинг ФИО
        Assert.Equal("Логинов", profile.LastName);
        Assert.Equal("Дмитрий Васильевич", profile.FirstName);
        Assert.Equal("Логинов Дмитрий Васильевич", profile.FullName);

        // Проверяем перенос очков и статистики
        Assert.Equal(406, profile.TotalRating);
        Assert.Equal(19, profile.TournamentsPlayed);
        Assert.Equal(1, profile.WinsCount);
        Assert.Equal(4, profile.Top3Count);
        Assert.Equal(27, profile.KnockoutsCount);
        Assert.Equal(6.0, profile.AvgPlace);
        Assert.Equal("1518", profile.ClubCardId);

        // Проверяем, что временный sheetUser удален
        var deletedSheetUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_12_abc123");
        Assert.Null(deletedSheetUser);
    }

    [Theory]
    [InlineData("Логинов Дмитрий Васильевич", "Дмитрий", "Логинов", true)]
    [InlineData("Логинов Дмитрий", "Дмитрий Васильевич", "Логинов", true)]
    [InlineData("Дмитрий Логинов", "Дмитрий", "Логинов", true)]
    [InlineData("Иванов Иван Иванович", "Петр", "Иванов", false)]
    [InlineData("Дмитрий", "Дмитрий", "Логинов", false)]
    [InlineData("Логинов", "Дмитрий", "Логинов", false)]
    public void IsSmartTokenMatch_VariousCombinations_BehavesCorrectly(string inputName, string targetFirst, string targetLast, bool expected)
    {
        var result = UsersController.IsSmartTokenMatch(inputName, targetFirst, targetLast);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetMe_WithPhoto200OrAvatarUrl_UpdatesUserAvatarInDb()
    {
        using var context = CreateInMemoryDbContext();
        var user = new User
        {
            VkId = "777888",
            FirstName = "Василий",
            LastName = "Лукашенко",
            AvatarUrl = null
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "777888";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var actionResult = await controller.GetMe(photo_200: "https://vk.com/photo777.jpg");
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("https://vk.com/photo777.jpg", profile.AvatarUrl);

        var dbUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "777888");
        Assert.NotNull(dbUser);
        Assert.Equal("https://vk.com/photo777.jpg", dbUser.AvatarUrl);

        // Обновление на новый URL
        var updatedResult = await controller.GetMe(avatarUrl: "https://vk.com/photo_new.jpg");
        var updatedOk = Assert.IsType<OkObjectResult>(updatedResult.Result);
        var updatedProfile = Assert.IsType<UserProfileDto>(updatedOk.Value);

        Assert.Equal("https://vk.com/photo_new.jpg", updatedProfile.AvatarUrl);

        var dbUserUpdated = await context.Users.FirstOrDefaultAsync(u => u.VkId == "777888");
        Assert.NotNull(dbUserUpdated);
        Assert.Equal("https://vk.com/photo_new.jpg", dbUserUpdated.AvatarUrl);
    }

    [Fact]
    public async Task UpdateProfile_WithMasterAdminCard_BypassesAntiTheftPhoneCheckAndGrantsAdmin()
    {
        using var context = CreateInMemoryDbContext();

        // Предположим, в таблице есть игрок с похожим именем и своим телефоном
        var sheetUser = new User
        {
            VkId = "sheet_admin_target",
            FirstName = "Иван",
            LastName = "Иванов",
            PhoneNumber = "+7 (999) 111-22-33",
            ClubCardId = "MC-ADMIN-MASTER-777-ACCESS-2026",
            TotalRating = 500
        };
        context.Users.Add(sheetUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "admin_non_club_member";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Админ вводит мастер-карту со своим произвольным телефоном
        var request = new UpdateProfileRequest(
            Nickname: "MasterAdmin",
            FullName: "Иванов Иван",
            PhoneNumber: "+7 (999) 888-77-66", // Не совпадает с sheetUser!
            ClubCardId: "MC-ADMIN-MASTER-777-ACCESS-2026"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.True(profile.IsAdmin);
        Assert.Equal("MC-ADMIN-MASTER-777-ACCESS-2026", profile.ClubCardId);
        Assert.Equal("MasterAdmin", profile.Nickname);
        Assert.Equal("+7 (999) 888-77-66", profile.PhoneNumber);

        // Проверяем, что в БД пользователь сохранен с правами админа
        var dbUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "admin_non_club_member");
        Assert.NotNull(dbUser);
        Assert.Equal("MC-ADMIN-MASTER-777-ACCESS-2026", dbUser.ClubCardId);

        // При последующем вызове GetMe пользователь по-прежнему является админом
        var meResult = await controller.GetMe();
        var meOk = Assert.IsType<OkObjectResult>(meResult.Result);
        var meProfile = Assert.IsType<UserProfileDto>(meOk.Value);
        Assert.True(meProfile.IsAdmin);
    }

    [Fact]
    public async Task UpdateProfile_WithMasterAdminCard_AllowsMultipleAdminsWithoutDuplicateError()
    {
        using var context = CreateInMemoryDbContext();

        // Первый админ уже использует мастер-карту
        var admin1 = new User
        {
            VkId = "admin_1",
            Nickname = "AdminOne",
            ClubCardId = "MC-ADMIN-MASTER-777-ACCESS-2026",
            TotalRating = 0
        };
        context.Users.Add(admin1);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "admin_2";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Второй админ вводит ту же самую мастер-карту
        var request = new UpdateProfileRequest(
            Nickname: "AdminTwo",
            FullName: "Петров Петр",
            PhoneNumber: "+7 (999) 222-33-44",
            ClubCardId: "  mc-admin-master-777-access-2026  " // Проверка trim и case-insensitivity
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.True(profile.IsAdmin);
        Assert.Equal("MC-ADMIN-MASTER-777-ACCESS-2026", profile.ClubCardId);
        Assert.Equal("AdminTwo", profile.Nickname);
    }

    [Fact]
    public async Task UpdateProfile_WithSecondaryMasterAdminCard_BypassesAntiTheftAndGrantsAdmin()
    {
        using var context = CreateInMemoryDbContext();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "secondary_admin";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            Nickname: "VipAdmin",
            FullName: "Сидоров Сидор",
            PhoneNumber: "+7 (999) 333-44-55",
            ClubCardId: "ADMIN-777-MONTE-CARLO-VIP-PASS"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.True(profile.IsAdmin);
        Assert.Equal("MC-ADMIN-MASTER-777-ACCESS-2026", profile.ClubCardId);
    }

    [Fact]
    public async Task VkAuthorizeAttribute_WithMasterCardUser_AuthorizesAdminAccess()
    {
        using var context = CreateInMemoryDbContext();
        var adminUser = new User
        {
            VkId = "master_vk_admin",
            FirstName = "Мастер",
            LastName = "Админ",
            ClubCardId = "MC-ADMIN-MASTER-777-ACCESS-2026"
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        var serviceProvider = new ServiceCollection()
            .AddScoped<IVkAuthValidator>(_ => new VkAuthValidator(
                Options.Create(new VkOptions { RequireValidation = false }),
                NullLogger<VkAuthValidator>.Instance))
            .AddScoped(_ => context)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Headers["X-Test-Vk-Id"] = "master_vk_admin";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        bool nextCalled = false;
        var filter = new VkAuthorizeAttribute { RequireAdmin = true };
        await filter.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled);
        Assert.Null(actionExecutingContext.Result);
        Assert.Equal(true, httpContext.Items["IsAdmin"]);
    }

    [Fact]
    public async Task VkAuthorizeAttribute_WithMasterCardUser_AllowsAccessEvenIfXIsAdminFalse()
    {
        using var context = CreateInMemoryDbContext();
        var adminUser = new User
        {
            VkId = "master_vk_admin_player_mode",
            FirstName = "Мастер",
            LastName = "Игрок",
            ClubCardId = "MC-ADMIN-MASTER-777-ACCESS-2026"
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        var serviceProvider = new ServiceCollection()
            .AddScoped<IVkAuthValidator>(_ => new VkAuthValidator(
                Options.Create(new VkOptions { RequireValidation = false }),
                NullLogger<VkAuthValidator>.Instance))
            .AddScoped(_ => context)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Headers["X-Test-Vk-Id"] = "master_vk_admin_player_mode";
        httpContext.Request.Headers["X-Is-Admin"] = "false"; // UI режим игрока, но права мастера в БД сохраняются

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        bool nextCalled = false;
        var filter = new VkAuthorizeAttribute { RequireAdmin = true };
        await filter.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        Assert.True(nextCalled);
        Assert.Null(actionExecutingContext.Result);
        Assert.Equal(true, httpContext.Items["IsAdmin"]);
    }

    [Fact]
    public async Task VkAuthorizeAttribute_RequireAdmin_WhenUserHasNormalCard_Returns403()
    {
        using var context = CreateInMemoryDbContext();
        var normalUser = new User
        {
            VkId = "regular_player",
            FirstName = "Обычный",
            LastName = "Игрок",
            ClubCardId = "1266"
        };
        context.Users.Add(normalUser);
        await context.SaveChangesAsync();

        var serviceProvider = new ServiceCollection()
            .AddScoped<IVkAuthValidator>(_ => new VkAuthValidator(
                Options.Create(new VkOptions { RequireValidation = false }),
                NullLogger<VkAuthValidator>.Instance))
            .AddScoped(_ => context)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Headers["X-Test-Vk-Id"] = "regular_player";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionExecutingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());

        bool nextCalled = false;
        var filter = new VkAuthorizeAttribute { RequireAdmin = true };
        await filter.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        });

        Assert.False(nextCalled);
        var objectResult = Assert.IsType<ObjectResult>(actionExecutingContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WhenFakeCardEnteredAndCardDbExists_ReturnsBadRequest400WithSpecificMessage()
    {
        using var context = CreateInMemoryDbContext();
        var validPlayer = new User
        {
            VkId = "sheet_card_1060",
            ClubCardId = "1060"
        };
        context.Users.Add(validPlayer);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "test_player_fake_card";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            Nickname: "Fake_Card_User",
            ClubCardId: "5555"
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var messageProp = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value)?.ToString();

        Assert.Equal("Клубный ID не найден в базе Monte Carlo. Напишите в сообщения сообщества для получения карты.", messageProp);
    }

    [Fact]
    public async Task UpdateProfile_WithMasterAdminCard_BypassesCardWhitelistCheck()
    {
        using var context = CreateInMemoryDbContext();
        var validPlayer = new User
        {
            VkId = "sheet_card_1060",
            ClubCardId = "1060"
        };
        context.Users.Add(validPlayer);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "test_admin_bypass";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            Nickname: "Super_Admin",
            ClubCardId: "MC-ADMIN-MASTER-777-ACCESS-2026"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.True(profile.IsAdmin);
        Assert.Equal("MC-ADMIN-MASTER-777-ACCESS-2026", profile.ClubCardId);
    }

    [Fact]
    public async Task UpdateProfile_WithValidWhitelistedCard_TransfersStatsAndSheetRank()
    {
        using var context = CreateInMemoryDbContext();
        var sheetUser = new User
        {
            VkId = "sheet_157_fedotova",
            LastName = "Федотова",
            FirstName = "Евгения",
            ClubCardId = "1200",
            SheetRank = 157,
            TotalRating = 10
        };
        context.Users.Add(sheetUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "real_fedotova_vk";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Федотова Евгения",
            Nickname: "Zhenya_Poker",
            ClubCardId: "1200"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(157, profile.SheetRank);
        Assert.Equal(10, profile.TotalRating);
        Assert.Equal("1200", profile.ClubCardId);
    }

    [Fact]
    public async Task UpdateProfile_WhenFakeCardEntered_AlwaysReturnsBadRequest400EvenIfNoOtherCardsInDb()
    {
        using var context = CreateInMemoryDbContext();
        // Database contains NO cards at all
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "test_player_fake_card_empty_db";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            Nickname: "Fake_Player",
            ClubCardId: "5555"
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var messageProp = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value)?.ToString();

        Assert.Equal("Клубный ID не найден в базе Monte Carlo. Напишите в сообщения сообщества для получения карты.", messageProp);
    }

    [Fact]
    public async Task UpdateProfile_ExistingCardHolder_CanUpdateNicknameWithoutCardError()
    {
        using var context = CreateInMemoryDbContext();
        var existingUser = new User
        {
            VkId = "existing_vk_user",
            FirstName = "Василий",
            LastName = "Лукашенко",
            ClubCardId = "1060",
            TotalRating = 449
        };
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "existing_vk_user";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            Nickname: "Vassily_Pro",
            ClubCardId: "1060"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("Vassily_Pro", profile.Nickname);
        Assert.Equal("1060", profile.ClubCardId);
    }

    [Fact]
    public async Task UpdateProfile_IgorGulyaev_VerifiedInRatingWithCard1080_SucceedsAndCarriesOverPoints504()
    {
        using var context = CreateInMemoryDbContext();

        // 1. В базе находится профиль sheet_* Игоря Гуляева из Google Sheets с 504 очками
        // и ошибочным ClubCardId = "5" (из-за бага смещения места в рейтинг-листе)
        var sheetUser = new User
        {
            VkId = "sheet_5_montecarlo_rating",
            FirstName = "Игорь",
            LastName = "Гуляев",
            TotalRating = 504,
            SeasonRating = 485,
            SheetRank = 5,
            ClubCardId = "5", // Ошибочно записанный ранг
            TournamentsPlayed = 26,
            WinsCount = 1,
            Top3Count = 6,
            Top10Count = 10,
            KnockoutsCount = 28,
            AvgPlace = 4.92,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        context.Users.Add(sheetUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_real_igor_gulyaev";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // 2. Реальный игрок Игорь Гуляев вводит свои реальные данные:
        // ФИО: Гуляев Игорь, Никнейм: Мэйби_Бэйби, Телефон: +7 (904) 847-31-61, Карта: 1080
        var request = new UpdateProfileRequest(
            FullName: "Гуляев Игорь",
            Nickname: "Мэйби_Бэйби",
            PhoneNumber: "+7 (904) 847-31-61",
            ClubCardId: "1080",
            AcceptedTerms: true
        );

        var actionResult = await controller.UpdateProfile(request);

        // Должен вернуть 200 OK (не блокироваться cardExists и не отсекаться по ложному ClubCardId = "5")
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        // Проверяем, что реальная клубная карта 1080 успешно привязана
        Assert.Equal("1080", profile.ClubCardId);
        Assert.Equal("Мэйби_Бэйби", profile.Nickname);
        Assert.Equal("+7 (904) 847-31-61", profile.PhoneNumber);
        Assert.Equal("Гуляев Игорь", profile.FullName);

        // Проверяем перенос накопленных 504 очков и статистики
        Assert.Equal(504, profile.TotalRating);
        Assert.Equal(485, profile.SeasonRating);
        Assert.Equal(5, profile.SheetRank);
        Assert.Equal(26, profile.TournamentsPlayed);
        Assert.Equal(1, profile.WinsCount);
        Assert.Equal(6, profile.Top3Count);
        Assert.Equal(10, profile.Top10Count);
        Assert.Equal(28, profile.KnockoutsCount);
        Assert.Equal(4.92, profile.AvgPlace);

        // Проверяем, что временный sheetUser удален из базы
        var remainingSheetUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_5_montecarlo_rating");
        Assert.Null(remainingSheetUser);

        // Проверяем данные в БД
        var dbUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "vk_real_igor_gulyaev");
        Assert.NotNull(dbUser);
        Assert.Equal("1080", dbUser.ClubCardId);
        Assert.Equal(504, dbUser.TotalRating);
        Assert.Equal(5, dbUser.SheetRank);
    }

    [Fact]
    public async Task UpdateProfile_UnverifiedPlayerEnteringUnknownCard_ReturnsBadRequest400()
    {
        using var context = CreateInMemoryDbContext();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_unknown_stranger";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Неизвестный пользователь (которого нет в рейтинге клуба) вводит несуществующую карту
        var request = new UpdateProfileRequest(
            FullName: "Незнакомцев Петр",
            Nickname: "Stranger",
            PhoneNumber: "+7 (900) 000-00-00",
            ClubCardId: "9999"
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var messageProp = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value)?.ToString()
            ?? badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Contains("Клубный ID не найден в базе Monte Carlo", messageProp);
    }

    [Fact]
    public async Task UpdateProfile_WhenCardAlreadyBoundToVerifiedRealUser_BlocksDuplicateRegistration()
    {
        using var context = CreateInMemoryDbContext();

        // Игорь Гуляев уже зарегистрирован с картой 1080
        var igor = new User
        {
            VkId = "vk_real_igor",
            FirstName = "Игорь",
            LastName = "Гуляев",
            ClubCardId = "1080",
            TotalRating = 504
        };
        context.Users.Add(igor);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_impostor";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Другой пользователь пытается указать ту же карту 1080
        var request = new UpdateProfileRequest(
            FullName: "Самозванец Иван",
            Nickname: "Impostor",
            PhoneNumber: "+7 (900) 111-22-33",
            ClubCardId: "1080"
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var messageProp = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value)?.ToString();
        Assert.Equal("Эта клубная карта уже привязана к другому профилю. Обратитесь к администратору клуба.", messageProp);
    }

    [Fact]
    public async Task UpdateProfile_PrioritizesRatedSheetUserOverZeroPointCardStub()
    {
        using var context = CreateInMemoryDbContext();

        // 1. Заглушка карты с 0 очков из белого списка
        var cardStub = new User
        {
            VkId = "sheet_card_1080_stub",
            FirstName = "Игорь",
            LastName = "Гуляев",
            ClubCardId = "1080",
            TotalRating = 0,
            TournamentsPlayed = 0
        };

        // 2. Настоящий профиль из рейтинга с 504 очками (но ошибочной картой 5)
        var ratingProfile = new User
        {
            VkId = "sheet_5_igor",
            FirstName = "Игорь",
            LastName = "Гуляев",
            ClubCardId = "5",
            SheetRank = 5,
            TotalRating = 504,
            SeasonRating = 485,
            TournamentsPlayed = 26
        };

        context.Users.AddRange(cardStub, ratingProfile);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_real_igor_stub_test";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Гуляев Игорь",
            Nickname: "Maybe_Baby",
            ClubCardId: "1080"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        // Должен перенестись профиль с 504 очками, а не заглушка с 0 очков!
        Assert.Equal(504, profile.TotalRating);
        Assert.Equal(485, profile.SeasonRating);
        Assert.Equal("1080", profile.ClubCardId);

        // Заглушка cardStub должна быть удалена, а рейтинг ratingProfile перенесен в новый профиль
        var dbCardStub = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_card_1080_stub");
        Assert.Null(dbCardStub);

        var dbRatingProfile = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_5_igor");
        Assert.Null(dbRatingProfile);

        var realUser = await context.Users.FirstOrDefaultAsync(u => u.VkId == "vk_real_igor_stub_test");
        Assert.NotNull(realUser);
        Assert.Equal(504, realUser.TotalRating);
        Assert.Equal("1080", realUser.ClubCardId);
    }

    [Fact]
    public async Task UpdateProfile_WhenCardBelongsToAnotherSheetRatingPlayer_ReturnsBadRequestAndDoesNotDeleteOtherPlayer()
    {
        using var context = CreateInMemoryDbContext();

        // Другой рейтинговый игрок имеет карту 1080
        var anotherRatedPlayer = new User
        {
            VkId = "sheet_77_alex",
            FirstName = "Алексей",
            LastName = "Иванов",
            ClubCardId = "1080",
            TotalRating = 300,
            TournamentsPlayed = 15
        };

        // Текущий игрок в рейтинге с картой null
        var currentRatedPlayer = new User
        {
            VkId = "sheet_5_igor",
            FirstName = "Игорь",
            LastName = "Гуляев",
            ClubCardId = null,
            SheetRank = 5,
            TotalRating = 504,
            TournamentsPlayed = 26
        };

        context.Users.AddRange(anotherRatedPlayer, currentRatedPlayer);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_real_igor";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Гуляев Игорь",
            Nickname: "Maybe_Baby",
            ClubCardId: "1080"
        );

        var actionResult = await controller.UpdateProfile(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        var messageProp = badRequest.Value?.GetType().GetProperty("Message")?.GetValue(badRequest.Value)?.ToString();

        Assert.Equal("Эта клубная карта уже привязана к другому профилю. Обратитесь к администратору клуба.", messageProp);

        // Другой рейтинговый игрок НЕ должен быть удален из БД!
        var stillExistingAlex = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_77_alex");
        Assert.NotNull(stillExistingAlex);
        Assert.Equal(300, stillExistingAlex.TotalRating);
    }

    [Fact]
    public async Task UpdateProfile_WhenDuplicateSheetHasCorruptedRankCard_CleansCorruptedCardWithoutDeletingUser()
    {
        using var context = CreateInMemoryDbContext();

        // Игрок на месте 1080, у которого из-за бага смещения ранга стоит ClubCardId = "1080"
        var playerRank1080 = new User
        {
            VkId = "sheet_1080_player",
            FirstName = "Константин",
            LastName = "Смирнов",
            SheetRank = 1080,
            ClubCardId = "1080",
            TotalRating = 12,
            TournamentsPlayed = 1
        };

        // Игорь Гуляев (ранг 5, карта "5")
        var igorSheet = new User
        {
            VkId = "sheet_5_igor",
            FirstName = "Игорь",
            LastName = "Гуляев",
            SheetRank = 5,
            ClubCardId = "5",
            TotalRating = 504,
            TournamentsPlayed = 26
        };

        context.Users.AddRange(playerRank1080, igorSheet);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_real_igor";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Гуляев Игорь",
            Nickname: "Maybe_Baby",
            ClubCardId: "1080"
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(504, profile.TotalRating);
        Assert.Equal("1080", profile.ClubCardId);

        // Игрок с рангом 1080 НЕ удален, но его ошибочный номер карты сброшен в null!
        var remainingPlayer1080 = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_1080_player");
        Assert.NotNull(remainingPlayer1080);
        Assert.Null(remainingPlayer1080.ClubCardId);
        Assert.Equal(12, remainingPlayer1080.TotalRating);
    }

    [Fact]
    public async Task UpdateProfile_WhenCandidateCardAlreadyClaimedByRealUser_ExcludesCandidateFromMatching()
    {
        using var context = CreateInMemoryDbContext();

        // Реальный пользователь уже владеет картой 1060
        var realLukashenko = new User
        {
            VkId = "vk_lukashenko",
            FirstName = "Василий",
            LastName = "Лукашенко",
            ClubCardId = "1060",
            TotalRating = 486
        };

        // Кандидат 1: "Иван Иванов" с картой 1060 (которая уже занята Василием Лукашенко)
        var candidateWithTakenCard = new User
        {
            VkId = "sheet_100_ivan_taken",
            FirstName = "Иван",
            LastName = "Иванов",
            ClubCardId = "1060",
            TotalRating = 100
        };

        // Кандидат 2: "Иван Иванов" без карты (валидный незанятый профиль)
        var candidateFree = new User
        {
            VkId = "sheet_101_ivan_free",
            FirstName = "Иван",
            LastName = "Иванов",
            ClubCardId = null,
            TotalRating = 50
        };

        context.Users.AddRange(realLukashenko, candidateWithTakenCard, candidateFree);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "vk_new_ivan";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Иванов Иван",
            Nickname: "IvanFree",
            ClubCardId: "1099" // Вводит свою свободную карту
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        // Кандидат с картой, занятой Лукашенко (100 очков), должен быть отфильтрован,
        // а выбран свободный кандидат (50 очков)!
        Assert.Equal(50, profile.TotalRating);
        Assert.Equal("1099", profile.ClubCardId);

        var dbCandidateFree = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_101_ivan_free");
        Assert.Null(dbCandidateFree);

        var dbCandidateTaken = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_100_ivan_taken");
        Assert.NotNull(dbCandidateTaken);
    }
}

