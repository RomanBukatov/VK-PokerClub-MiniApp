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
            PhoneNumber: "+7 (999) 111-22-33"
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
}
