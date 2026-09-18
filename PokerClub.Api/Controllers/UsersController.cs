using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PokerClub.Api.DTOs;
using PokerClub.Api.Extensions;
using PokerClub.Api.Filters;
using PokerClub.Api.Models;
using PokerClub.Api.Services;
using PokerClub.Domain.Entities;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IVkAuthValidator? _vkAuthValidator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(AppDbContext context, ILogger<UsersController> logger, IVkAuthValidator? vkAuthValidator = null)
    {
        _context = context;
        _logger = logger;
        _vkAuthValidator = vkAuthValidator;
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

        bool isAdmin = CheckIsAdmin(currentVkId);
        return Ok(ToProfileDto(user, isAdmin));
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

        var requestedCardId = request.ClubCardId?.Trim();
        var requestedPhone = request.PhoneNumber?.Trim();

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = requestedPhone;
        }

        if (!string.IsNullOrEmpty(requestedCardId))
        {
            // Если пользователь уже привязал эту карту ранее — повторная проверка не требуется
            bool alreadyOwnsCard = !string.IsNullOrWhiteSpace(user.ClubCardId) &&
                                   string.Equals(user.ClubCardId.Trim(), requestedCardId, StringComparison.OrdinalIgnoreCase);

            if (!alreadyOwnsCard)
            {
                // Правило 2: Защита от дубликатов (1 карта = 1 аккаунт)
                var isCardTakenByRealUser = await _context.Users.AnyAsync(u => 
                    u.ClubCardId != null &&
                    u.ClubCardId.ToLower() == requestedCardId.ToLower() &&
                    u.Id != user.Id &&
                    !u.VkId.StartsWith("sheet_"));

                if (isCardTakenByRealUser)
                {
                    return BadRequest(new { Message = "Эта клубная карта уже привязана к другому профилю. Обратитесь к администратору клуба." });
                }

                // Правило 3: Защита от угона чужого рейтинга (Сверка телефона)
                var sheetUser = await _context.Users.FirstOrDefaultAsync(u => 
                    u.ClubCardId != null &&
                    u.ClubCardId.ToLower() == requestedCardId.ToLower() &&
                    u.Id != user.Id &&
                    u.VkId.StartsWith("sheet_"));

                if (sheetUser != null)
                {
                    // Сверка телефона владельца карты в базе
                    if (!string.IsNullOrWhiteSpace(sheetUser.PhoneNumber))
                    {
                        var cardPhone10 = NormalizePhone(sheetUser.PhoneNumber);
                        var userPhone10 = NormalizePhone(requestedPhone ?? user.PhoneNumber);

                        if (string.IsNullOrEmpty(userPhone10) || cardPhone10 != userPhone10)
                        {
                            return BadRequest(new { Message = "Указанный номер телефона не совпадает с телефоном владельца карты в базе клуба. Если это ваша карта — обратитесь к администратору." });
                        }
                    }

                    // Успешная верификация: перенос накопленных очков и статистики
                    user.TotalRating = sheetUser.TotalRating;
                    user.SeasonRating = sheetUser.SeasonRating;
                    user.TournamentsPlayed = sheetUser.TournamentsPlayed;
                    user.WinsCount = sheetUser.WinsCount;
                    user.Top3Count = sheetUser.Top3Count;
                    user.Top10Count = sheetUser.Top10Count;
                    user.KnockoutsCount = sheetUser.KnockoutsCount;
                    user.AvgPlace = sheetUser.AvgPlace;

                    // Перепривязываем регистрации в турнирах
                    var sheetRegs = await _context.Registrations.Where(r => r.UserId == sheetUser.Id).ToListAsync();
                    foreach (var reg in sheetRegs)
                    {
                        reg.UserId = user.Id;
                    }

                    _context.Users.Remove(sheetUser);
                    _logger.LogInformation("Успешно привязана клубная карта {CardId} к пользователю {VkId}: перенесено {Points} очков", requestedCardId, user.VkId, user.TotalRating);
                }

                user.ClubCardId = requestedCardId;
            }
        }
        else if (request.ClubCardId != null)
        {
            // Правило 1: Пользователь явно очистил поле карты
            user.ClubCardId = null;
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

        await _context.SaveChangesAsync();
        bool isAdmin = CheckIsAdmin(currentVkId);
        return Ok(ToProfileDto(user, isAdmin));
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

    private bool CheckIsAdmin(string vkId)
    {
        if (string.IsNullOrWhiteSpace(vkId))
            return false;

        if (HttpContext?.Items.TryGetValue(HttpContextExtensions.IsAdminItemKey, out var val) == true && val is bool isAdmin)
        {
            return isAdmin;
        }

        var validator = _vkAuthValidator ?? HttpContext?.RequestServices?.GetService<IVkAuthValidator>();
        if (validator != null)
        {
            return validator.IsConfiguredAdmin(vkId);
        }

        var vkOptions = HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Options.IOptions<VkOptions>>()?.Value;
        if (vkOptions?.AdminVkIds != null)
        {
            var cleanTarget = VkAuthValidator.NormalizeVkId(vkId);
            return vkOptions.AdminVkIds.Any(id => string.Equals(VkAuthValidator.NormalizeVkId(id), cleanTarget, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static UserProfileDto ToProfileDto(User user, bool isAdmin = false)
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
            user.SeasonRating,
            isAdmin
        );
    }

    public static string CalculateClubStatus(int rating) => rating switch
    {
        <= 100 => "Newbie",
        <= 250 => "Fish",
        <= 400 => "Reg",
        _ => "Pro"
    };

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = Regex.Replace(phone, @"\D", "");
        return digits.Length >= 10 ? digits[^10..] : digits;
    }
}
