using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PokerClub.Api.DTOs;
using PokerClub.Api.Extensions;
using PokerClub.Api.Filters;
using PokerClub.Api.Models;
using PokerClub.Api.Services;
using PokerClub.Domain.Constants;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Services;
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
    [HttpGet("profile")]
    [VkAuthorize]
    public async Task<ActionResult<UserProfileDto>> GetMe(
        [FromQuery] string? photo_200 = null,
        [FromQuery] string? avatarUrl = null)
    {
        var currentVkId = HttpContext.GetVkUserId();
        if (string.IsNullOrWhiteSpace(currentVkId))
        {
            return Unauthorized(new { Message = "Пользователь не авторизован." });
        }

        // Извлекаем переданный аватар из query, headers или telegram данных
        string? incomingAvatarUrl = !string.IsNullOrWhiteSpace(photo_200) ? photo_200.Trim() : null;
        if (string.IsNullOrWhiteSpace(incomingAvatarUrl) && !string.IsNullOrWhiteSpace(avatarUrl))
        {
            incomingAvatarUrl = avatarUrl.Trim();
        }
        if (string.IsNullOrWhiteSpace(incomingAvatarUrl) && HttpContext.Request.Query.TryGetValue("avatar_url", out var qAvatar))
        {
            incomingAvatarUrl = qAvatar.ToString().Trim();
        }
        if (string.IsNullOrWhiteSpace(incomingAvatarUrl) && HttpContext.Request.Headers.TryGetValue("X-Avatar-Url", out var hAvatar))
        {
            incomingAvatarUrl = hAvatar.ToString().Trim();
        }
        if (string.IsNullOrWhiteSpace(incomingAvatarUrl) && HttpContext.Request.Headers.TryGetValue("X-VK-Photo", out var hVkPhoto))
        {
            incomingAvatarUrl = hVkPhoto.ToString().Trim();
        }
        if (string.IsNullOrWhiteSpace(incomingAvatarUrl) && HttpContext.Request.Headers.TryGetValue("X-Telegram-User", out var tgUserJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(tgUserJson.ToString());
                if (doc.RootElement.TryGetProperty("photo_url", out var pu)) incomingAvatarUrl = pu.GetString()?.Trim();
            }
            catch { }
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == currentVkId);
        if (user == null)
        {
            string firstName = "Игрок";
            string lastName = "";

            if (HttpContext.Request.Headers.TryGetValue("X-Telegram-User", out var tgUserJsonCreate))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(tgUserJsonCreate.ToString());
                    if (doc.RootElement.TryGetProperty("first_name", out var fn)) firstName = fn.GetString() ?? firstName;
                    if (doc.RootElement.TryGetProperty("last_name", out var ln)) lastName = ln.GetString() ?? lastName;
                }
                catch { }
            }

            // Автоматически создаем начальный профиль
            user = new User
            {
                VkId = currentVkId,
                FirstName = firstName,
                LastName = lastName,
                AvatarUrl = incomingAvatarUrl,
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
        else
        {
            // Если от VK Bridge передано поле с аватаром (photo_200 / avatarUrl),
            // автоматически обновляй user.AvatarUrl в базе данных, если оно пустое или изменилось.
            if (!string.IsNullOrWhiteSpace(incomingAvatarUrl) &&
                (string.IsNullOrWhiteSpace(user.AvatarUrl) || user.AvatarUrl != incomingAvatarUrl))
            {
                user.AvatarUrl = incomingAvatarUrl;
                await _context.SaveChangesAsync();
            }
        }

        bool isAdmin = CheckIsAdmin(currentVkId) || MasterClubCardConstants.IsMasterAdminCard(user.ClubCardId);
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

        if (string.IsNullOrWhiteSpace(request.ClubCardId))
        {
            return BadRequest(new { message = "Клубный ID обязателен для регистрации и привязки профиля. Получите его в группе клуба." });
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

        string? inputFullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(inputFullName))
        {
            if (!string.IsNullOrWhiteSpace(request.LastName) || !string.IsNullOrWhiteSpace(request.FirstName))
            {
                inputFullName = $"{request.LastName} {request.FirstName}".Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(inputFullName))
        {
            var parts = inputFullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
        else
        {
            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName.Trim();
            }
        }

        // Поиск связанного профиля sheet_* из Google Sheets (пропускаем для мастер-карты администратора)
        User? sheetUser = null;
        string? searchName = inputFullName ?? $"{user.LastName} {user.FirstName}".Trim();
        bool isMasterCard = MasterClubCardConstants.IsMasterAdminCard(requestedCardId);

        if (!isMasterCard)
        {
            // 1. Поиск по Smart Token Matching имени среди sheet_* игроков
            if (!string.IsNullOrWhiteSpace(searchName))
            {
                var candidateSheetUsers = await _context.Users
                    .Where(u => u.VkId.StartsWith("sheet_") && u.Id != user.Id)
                    .ToListAsync();

                sheetUser = candidateSheetUsers
                    .Where(u => (u.ClubCardId == null || string.Equals(u.ClubCardId, requestedCardId, StringComparison.OrdinalIgnoreCase)) &&
                                IsSmartTokenMatch(searchName, u.FirstName, u.LastName))
                    .OrderByDescending(u => ExtractTokens($"{u.LastName} {u.FirstName}").SetEquals(ExtractTokens(searchName)))
                    .ThenByDescending(u => u.TotalRating)
                    .FirstOrDefault();
            }

            // 2. Если по имени не найден, но введена карта — ищем по ClubCardId
            bool matchedByName = sheetUser != null;
            if (sheetUser == null && !string.IsNullOrEmpty(requestedCardId))
            {
                sheetUser = await _context.Users.FirstOrDefaultAsync(u =>
                    u.ClubCardId != null &&
                    u.ClubCardId.ToLower() == requestedCardId.ToLower() &&
                    u.Id != user.Id &&
                    u.VkId.StartsWith("sheet_"));
            }

            if (!string.IsNullOrEmpty(requestedCardId))
            {
                // Проверка на валидность клубной карты по базе Monte Carlo (белый список карт)
                var cardExists = await _context.Users.AnyAsync(u =>
                    u.ClubCardId != null &&
                    u.ClubCardId.ToLower() == requestedCardId.ToLower());

                if (!cardExists)
                {
                    return BadRequest(new { Message = "Клубный ID не найден в базе Monte Carlo. Напишите в сообщения сообщества для получения карты." });
                }

                // Проверка на дубликаты среди РЕАЛЬНЫХ пользователей (1 карта = 1 аккаунт)
                bool alreadyOwnsCard = !string.IsNullOrWhiteSpace(user.ClubCardId) &&
                                       string.Equals(user.ClubCardId.Trim(), requestedCardId, StringComparison.OrdinalIgnoreCase);

                if (!alreadyOwnsCard)
                {
                    var isCardTakenByRealUser = await _context.Users.AnyAsync(u => 
                        u.ClubCardId != null &&
                        u.ClubCardId.ToLower() == requestedCardId.ToLower() &&
                        u.Id != user.Id &&
                        !u.VkId.StartsWith("sheet_"));

                    if (isCardTakenByRealUser)
                    {
                        return BadRequest(new { Message = "Эта клубная карта уже привязана к другому профилю. Обратитесь к администратору клуба." });
                    }
                }
            }

            if (sheetUser != null)
            {
                // Если игрок был найден ИСКЛЮЧИТЕЛЬНО по карте (а не по имени), проверяем телефон для защиты от угона
                if (!matchedByName && !string.IsNullOrWhiteSpace(sheetUser.PhoneNumber))
                {
                    var cardPhone10 = NormalizePhone(sheetUser.PhoneNumber);
                    var userPhone10 = NormalizePhone(requestedPhone ?? user.PhoneNumber);

                    if (string.IsNullOrEmpty(userPhone10) || cardPhone10 != userPhone10)
                    {
                        return BadRequest(new { Message = "Указанный номер телефона не совпадает с телефоном владельца карты в базе клуба. Если это ваша карта — обратитесь к администратору." });
                    }
                }

                // Перенос накопленных очков и статистики из таблицы
                user.TotalRating = sheetUser.TotalRating;
                user.SeasonRating = sheetUser.SeasonRating;
                user.SheetRank = sheetUser.SheetRank;
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
                _logger.LogInformation("Успешно привязан профиль {SheetVkId} к пользователю {VkId}: перенесено {Points} очков (поиск по имени={MatchedByName})", sheetUser.VkId, user.VkId, user.TotalRating, matchedByName);
            }
        }

        if (!string.IsNullOrEmpty(requestedCardId))
        {
            user.ClubCardId = isMasterCard ? MasterClubCardConstants.MasterClubCardId : requestedCardId;
        }
        else if (request.ClubCardId != null)
        {
            user.ClubCardId = null;
        }
        else if (sheetUser != null && !string.IsNullOrEmpty(sheetUser.ClubCardId) && string.IsNullOrEmpty(user.ClubCardId))
        {
            user.ClubCardId = sheetUser.ClubCardId;
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
        if (isMasterCard)
        {
            HttpContext.Items["IsAdmin"] = true;
        }
        bool isAdmin = CheckIsAdmin(currentVkId) || isMasterCard || MasterClubCardConstants.IsMasterAdminCard(user.ClubCardId);
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

    [HttpGet("{id}/public-profile")]
    public async Task<ActionResult<PublicUserProfileDto>> GetPublicProfile(string id)
    {
        User? user = null;
        if (int.TryParse(id, out var intId))
        {
            user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == intId);
        }

        if (user == null)
        {
            user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.VkId == id);
        }

        if (user == null)
        {
            return NotFound(new { Message = "Игрок не найден." });
        }

        var httpContext = HttpContext;
        var currentVkId = httpContext?.GetVkUserId();
        bool isAdmin = (httpContext != null && httpContext.IsVkAdmin()) || (!string.IsNullOrWhiteSpace(currentVkId) && CheckIsAdmin(currentVkId));

        if (httpContext?.Request.Headers.TryGetValue("X-Is-Admin", out var adminHeader) == true)
        {
            var headerVal = adminHeader.ToString().Trim();
            if (string.Equals(headerVal, "false", StringComparison.OrdinalIgnoreCase) || headerVal == "0")
            {
                isAdmin = false;
            }
        }

        bool isOwner = !string.IsNullOrWhiteSpace(currentVkId) && 
            (!string.IsNullOrWhiteSpace(user.VkId) && (
                string.Equals(currentVkId, user.VkId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(VkAuthValidator.NormalizeVkId(currentVkId), VkAuthValidator.NormalizeVkId(user.VkId), StringComparison.OrdinalIgnoreCase)
            ));

        string? clubCardId = (isAdmin || isOwner) ? user.ClubCardId : null;
        string? phoneNumber = (isAdmin || isOwner) ? user.PhoneNumber : null;

        return Ok(new PublicUserProfileDto(
            user.Id,
            user.Nickname,
            user.FirstName,
            user.LastName,
            user.SeasonRating,
            user.TotalRating,
            user.TournamentsPlayed,
            user.WinsCount,
            user.Top3Count,
            user.Top10Count,
            user.KnockoutsCount,
            user.AvgPlace,
            user.AvatarUrl,
            user.VkId,
            clubCardId,
            phoneNumber,
            user.SheetRank
        ));
    }

    private bool CheckIsAdmin(string vkId)
    {
        if (string.IsNullOrWhiteSpace(vkId))
            return false;

        if (HttpContext?.Items.TryGetValue(HttpContextExtensions.IsAdminItemKey, out var val) == true && val is bool isAdmin)
        {
            return isAdmin;
        }

        var user = _context.Users.AsNoTracking().FirstOrDefault(u => u.VkId == vkId);
        if (user != null && MasterClubCardConstants.IsMasterAdminCard(user.ClubCardId))
        {
            return true;
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
        string fullName;
        if (!string.IsNullOrWhiteSpace(user.LastName) && !string.IsNullOrWhiteSpace(user.FirstName))
        {
            fullName = $"{user.LastName} {user.FirstName}".Trim();
        }
        else if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            fullName = user.LastName.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            fullName = user.FirstName.Trim();
        }
        else
        {
            fullName = user.Nickname ?? "Игрок";
        }

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
            isAdmin,
            user.SheetRank
        );
    }

    public static HashSet<string> ExtractTokens(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cleaned = Regex.Replace(input.ToLowerInvariant(), @"[^a-zа-яё0-9\s]", " ");
        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsSmartTokenMatch(string inputName, string? targetFirstName, string? targetLastName)
    {
        var inputTokens = ExtractTokens(inputName);
        var targetTokens = ExtractTokens($"{targetLastName} {targetFirstName}");

        if (inputTokens.Count == 0 || targetTokens.Count == 0)
            return false;

        // Точное совпадение наборов токенов
        if (inputTokens.SetEquals(targetTokens))
            return true;

        // Если одно является подмножеством другого (например, "Логинов Дмитрий" входит в "Логинов Дмитрий Васильевич")
        var intersection = new HashSet<string>(inputTokens, StringComparer.OrdinalIgnoreCase);
        intersection.IntersectWith(targetTokens);

        if (intersection.Count >= 2 && (inputTokens.IsSubsetOf(targetTokens) || targetTokens.IsSubsetOf(inputTokens)))
        {
            return true;
        }

        return false;
    }

    public static string CalculateClubStatus(int rating) => RankService.CalculateClubStatus(rating);

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = Regex.Replace(phone, @"\D", "");
        return digits.Length >= 10 ? digits[^10..] : digits;
    }
}
