using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PokerClub.Api.Controllers;
using PokerClub.Api.DTOs;
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
        Assert.Equal("Newbie", profile.Status);
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
        Assert.Equal("Fish", profile.Status); // 250 rating is Fish
    }

    [Fact]
    public async Task UpdateProfile_ShortNickname_ReturnsBadRequest()
    {
        using var context = CreateInMemoryDbContext();
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "888";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(Nickname: "ab"); // < 3 chars

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
        Assert.Equal("Pro", profile.Status); // 486 is Pro (401+)
        Assert.Equal("1", profile.ClubCardId);

        // Placeholder sheetUser was removed
        var remainingUsers = await context.Users.ToListAsync();
        Assert.Single(remainingUsers);
        Assert.Equal("real_tg_user_1", remainingUsers[0].VkId);
    }

    [Fact]
    public async Task UpdateProfile_WithoutCard_SucceedsWithZeroPointsAndNewbieStatus()
    {
        using var context = CreateInMemoryDbContext();
        // В базе есть игрок из таблицы с очками, но без карты
        var sheetUser = new User
        {
            VkId = "sheet_2_xyz999",
            FirstName = "Василий",
            LastName = "Лукашенко",
            TotalRating = 500,
            ClubCardId = "999"
        };
        context.Users.Add(sheetUser);
        await context.SaveChangesAsync();

        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "newbie_user_1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Новичок регистрируется без карты
        var request = new UpdateProfileRequest(
            FullName: "Лукашенко Василий",
            Nickname: "Newbie_Vasya",
            PhoneNumber: "+7 (915) 000-11-22",
            ClubCardId: null
        );

        var actionResult = await controller.UpdateProfile(request);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal(0, profile.TotalRating);
        Assert.Equal(0, profile.SeasonRating);
        Assert.Equal("Newbie", profile.Status);
        Assert.Null(profile.ClubCardId);

        // Старый sheetUser не затронут
        var sheetInDb = await context.Users.FirstOrDefaultAsync(u => u.VkId == "sheet_2_xyz999");
        Assert.NotNull(sheetInDb);
        Assert.Equal(500, sheetInDb.TotalRating);
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
        Assert.Equal("Reg", profile.Status); // 350 is Reg (251-400)

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
        var controller = new UsersController(context, NullLogger<UsersController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Test-Vk-Id"] = "single_word_user";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new UpdateProfileRequest(
            FullName: "Иван",
            Nickname: "IvanSuper"
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
    [InlineData(0, "Newbie")]
    [InlineData(50, "Newbie")]
    [InlineData(100, "Newbie")]
    [InlineData(101, "Fish")]
    [InlineData(250, "Fish")]
    [InlineData(251, "Reg")]
    [InlineData(400, "Reg")]
    [InlineData(401, "Pro")]
    [InlineData(486, "Pro")]
    [InlineData(1000, "Pro")]
    public void CalculateClubStatus_CalibratedThresholds_ReturnsCorrectStatus(int rating, string expectedStatus)
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
            AcceptedTerms = true
        });
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var profile = Assert.IsType<UserProfileDto>(okResult.Value);

        Assert.Equal("308885723", profile.VkId);
        Assert.True(profile.IsAdmin);
    }
}
