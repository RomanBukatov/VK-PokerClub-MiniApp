using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PokerClub.Api.DTOs;
using PokerClub.Api.Extensions;
using PokerClub.Api.Filters;
using PokerClub.Domain.Entities;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(AppDbContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("me")]
    [VkAuthorize]
    public async Task<ActionResult<UserProfileDto>> GetMe()
    {
        var currentVkId = HttpContext.GetVkUserId();
        if (string.IsNullOrWhiteSpace(currentVkId))
        {
            return Unauthorized(new { Message = "Пользователь не авторизован." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == currentVkId);
        if (user == null)
        {
            string firstName = "Игрок";
            string lastName = "";
            string? avatarUrl = null;

            if (HttpContext.Request.Headers.TryGetValue("X-Telegram-User", out var tgUserJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(tgUserJson.ToString());
                    if (doc.RootElement.TryGetProperty("first_name", out var fn)) firstName = fn.GetString() ?? firstName;
                    if (doc.RootElement.TryGetProperty("last_name", out var ln)) lastName = ln.GetString() ?? lastName;
                    if (doc.RootElement.TryGetProperty("photo_url", out var pu)) avatarUrl = pu.GetString();
                }
                catch { }
            }

            // Автоматически создаем начальный профиль
            user = new User
            {
                VkId = currentVkId,
                FirstName = firstName,
                LastName = lastName,
                AvatarUrl = avatarUrl,
                TotalRating = 0,
                SeasonRating = 0,
                TournamentsPlayed = 0,
                WinsCount = 0,
                Top3Count = 0,
                Top10Count = 0,
                KnockoutsCount = 0,
                AvgPlace = 0.0,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        return Ok(ToProfileDto(user));
    }

    [HttpPost("profile")]
    [HttpPut("profile")]
    [VkAuthorize]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var currentVkId = HttpContext.GetVkUserId();
        if (string.IsNullOrWhiteSpace(currentVkId))
        {
            return Unauthorized(new { Message = "Пользователь не авторизован." });
        }

        if (!string.IsNullOrWhiteSpace(request.Nickname))
        {
            var trimmedNick = request.Nickname.Trim();
            if (trimmedNick.Length < 3 || trimmedNick.Length > 20)
            {
                return BadRequest(new { Message = "Игровой никнейм должен содержать от 3 до 20 символов." });
            }

            // Допускаем буквы, цифры, дефисы и подчеркивания
            if (!Regex.IsMatch(trimmedNick, @"^[a-zA-Z0-9а-яА-ЯёЁ_\-]+$"))
            {
                return BadRequest(new { Message = "Никнейм может содержать только буквы, цифры и подчеркивание." });
            }
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == currentVkId);
        if (user == null)
        {
            user = new User
            {
                VkId = currentVkId,
                FirstName = "Игрок",
                LastName = "",
                TotalRating = 0,
                SeasonRating = 0,
                TournamentsPlayed = 0,
                WinsCount = 0,
                Top3Count = 0,
                Top10Count = 0,
                KnockoutsCount = 0,
                AvgPlace = 0.0,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
        }

        if (request.Nickname != null)
        {
            user.Nickname = request.Nickname.Trim();
        }

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = request.PhoneNumber.Trim();
        }

        if (request.ClubCardId != null)
        {
            user.ClubCardId = request.ClubCardId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            var parts = request.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                user.FirstName = parts[0];
                user.LastName = "";
            }
            else if (parts.Length > 1)
            {
                user.LastName = parts[0];
                user.FirstName = string.Join(" ", parts.Skip(1));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            user.FirstName = request.FirstName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            user.LastName = request.LastName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            user.AvatarUrl = request.AvatarUrl.Trim();
        }

        if (request.AcceptedTerms == true || request.AcceptedTermsAt.HasValue)
        {
            user.AcceptedTermsAt = request.AcceptedTermsAt ?? DateTime.UtcNow;
        }

        // Если у пользователя еще 0 очков, проверяем наличие импортированной записи из Google Sheets по имени или номеру карты
        if (user.TotalRating == 0 && user.TournamentsPlayed == 0)
        {
            var fullName = $"{user.LastName} {user.FirstName}".Trim();
            var sheetUsers = await _context.Users
                .Where(u => u.VkId.StartsWith("sheet_") && u.Id != user.Id)
                .ToListAsync();

            var match = GoogleSheetsSyncService.FindMatchingUser(sheetUsers, fullName, user.ClubCardId);
            if (match != null)
            {
                user.SeasonRating = match.SeasonRating;
                user.TotalRating = match.TotalRating;
                user.TournamentsPlayed = match.TournamentsPlayed;
                user.WinsCount = match.WinsCount;
                user.Top3Count = match.Top3Count;
                user.Top10Count = match.Top10Count;
                user.KnockoutsCount = match.KnockoutsCount;
                user.AvgPlace = match.AvgPlace;
                if (string.IsNullOrWhiteSpace(user.ClubCardId) && !string.IsNullOrWhiteSpace(match.ClubCardId))
                {
                    user.ClubCardId = match.ClubCardId;
                }
                if (string.IsNullOrWhiteSpace(user.PhoneNumber) && !string.IsNullOrWhiteSpace(match.PhoneNumber))
                {
                    user.PhoneNumber = match.PhoneNumber;
                }

                _context.Users.Remove(match);
                _logger.LogInformation("Объединен профиль пользователя {VkId} с данными из таблицы: {Points} очков", user.VkId, user.TotalRating);
            }
        }

        await _context.SaveChangesAsync();
        return Ok(ToProfileDto(user));
    }

    [HttpPost("accept-terms")]
    [VkAuthorize]
    public async Task<IActionResult> AcceptTerms([FromBody] AcceptTermsRequest? request)
    {
        var currentVkId = HttpContext.GetVkUserId();
        if (string.IsNullOrWhiteSpace(currentVkId))
        {
            return Unauthorized(new { Message = "Пользователь не авторизован." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == currentVkId);
        if (user == null)
        {
            user = new User
            {
                VkId = currentVkId,
                FirstName = "Игрок",
                LastName = "",
                TotalRating = 0,
                SeasonRating = 0,
                TournamentsPlayed = 0,
                WinsCount = 0,
                Top3Count = 0,
                Top10Count = 0,
                KnockoutsCount = 0,
                AvgPlace = 0.0,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
        }

        user.AcceptedTermsAt = request?.AcceptedAt ?? DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            Success = true, 
            AcceptedTermsAt = user.AcceptedTermsAt,
            Message = "Условия оферты и согласие 152-ФЗ успешно приняты." 
        });
    }

    private static UserProfileDto ToProfileDto(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName)) fullName = user.Nickname ?? "Игрок";

        var status = CalculateClubStatus(user.TotalRating);

        return new UserProfileDto(
            user.Id,
            user.VkId,
            user.FirstName,
            user.LastName,
            fullName,
            user.Nickname,
            user.PhoneNumber,
            user.ClubCardId,
            user.AvatarUrl,
            user.TotalRating,
            status,
            user.AcceptedTermsAt,
            user.TournamentsPlayed,
            user.WinsCount,
            user.Top3Count,
            user.Top10Count,
            user.KnockoutsCount,
            user.AvgPlace,
            user.CreatedAt,
            user.SeasonRating
        );
    }

    public static string CalculateClubStatus(int rating) => rating switch
    {
        <= 100 => "Newbie",
        <= 250 => "Fish",
        <= 400 => "Reg",
        _ => "Pro"
    };
}
