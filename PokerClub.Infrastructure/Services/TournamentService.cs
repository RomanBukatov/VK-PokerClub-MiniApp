using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;

namespace PokerClub.Infrastructure.Services;

public class TournamentService : ITournamentService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<TournamentService>? _logger;
    private readonly HttpClient? _httpClient;

    public TournamentService(AppDbContext context)
        : this(context, null, null, null)
    {
    }

    public TournamentService(
        AppDbContext context,
        IConfiguration? configuration = null,
        ILogger<TournamentService>? logger = null,
        HttpClient? httpClient = null)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    private static readonly HttpClient _defaultHttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private HttpClient GetHttpClient()
    {
        return _httpClient ?? _defaultHttpClient;
    }

    public async Task<List<Tournament>> GetScheduleAsync(int? cityId, int? clubId, bool includeFinished = false)
    {
        var query = _context.Tournaments
            .AsNoTracking()
            .Include(t => t.Club)
                .ThenInclude(c => c!.City)
            .Include(t => t.Registrations)
                .ThenInclude(r => r.User)
            .AsQueryable();

        if (!includeFinished)
        {
            query = query.Where(t => t.Status == TournamentStatus.Announced || 
                                     t.Status == TournamentStatus.RegistrationOpen);
        }

        if (clubId.HasValue && clubId.Value > 0)
        {
            query = query.Where(t => t.ClubId == clubId.Value);
        }
        else if (cityId.HasValue && cityId.Value > 0)
        {
            query = query.Where(t => t.Club != null && t.Club.CityId == cityId.Value);
        }

        if (includeFinished)
        {
            return await query.OrderByDescending(t => t.StartTime).ToListAsync();
        }

        return await query.OrderBy(t => t.StartTime).ToListAsync();
    }

    public async Task<Tournament?> GetTournamentByIdAsync(int id)
    {
        return await _context.Tournaments
            .AsNoTracking()
            .Include(t => t.Club)
                .ThenInclude(c => c!.City)
            .Include(t => t.Registrations.Where(r => r.Status == RegStatus.Active || r.Status == RegStatus.Played))
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<Tournament>> GetUserTournamentsAsync(string vkId)
    {
        return await _context.Tournaments
            .AsNoTracking()
            .Include(t => t.Club)
                .ThenInclude(c => c!.City)
            .Include(t => t.Registrations)
                .ThenInclude(r => r.User)
            .Where(t => t.Registrations.Any(r => r.User != null && r.User.VkId == vkId && r.Status != RegStatus.Canceled))
            .OrderByDescending(t => t.StartTime)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> RegisterPlayerAsync(
        int tournamentId, 
        string vkId,
        string? firstName = null,
        string? lastName = null,
        string? avatarUrl = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Атомарно лочим строку турнира с ограничением lock_timeout (защита от вечного ожидания)
            if (_context.Database.IsRelational())
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5000';");
                }
                catch
                {
                    // Игнорируем, если провайдер БД не поддерживает lock_timeout
                }

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT \"Id\" FROM \"Tournaments\" WHERE \"Id\" = {tournamentId} FOR UPDATE");
            }

            // 2. Загружаем турнир со всеми связями стандартным EF Core запросом в той же транзакции
            var tournament = await _context.Tournaments
                .Include(t => t.Registrations)
                .Include(t => t.Club)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null)
                return (false, "Турнир не найден.");

            if (tournament.Status != TournamentStatus.RegistrationOpen)
                return (false, "Регистрация на этот турнир закрыта или еще не началась.");

            // 3. Ищем или создаем юзера с сохранением реальных данных из VK
            var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == vkId);
            if (user == null)
            {
                user = new User
                {
                    VkId = vkId,
                    FirstName = !string.IsNullOrWhiteSpace(firstName) ? firstName.Trim() : "Игрок",
                    LastName = !string.IsNullOrWhiteSpace(lastName) ? lastName.Trim() : "VK",
                    AvatarUrl = avatarUrl?.Trim(),
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
                // Обновляем актуальные данные профиля, если они переданы
                bool updated = false;
                if (!string.IsNullOrWhiteSpace(firstName) && user.FirstName != firstName.Trim())
                {
                    user.FirstName = firstName.Trim();
                    updated = true;
                }
                if (!string.IsNullOrWhiteSpace(lastName) && user.LastName != lastName.Trim())
                {
                    user.LastName = lastName.Trim();
                    updated = true;
                }
                if (!string.IsNullOrWhiteSpace(avatarUrl) && user.AvatarUrl != avatarUrl.Trim())
                {
                    user.AvatarUrl = avatarUrl.Trim();
                    updated = true;
                }
                if (updated)
                {
                    await _context.SaveChangesAsync();
                }
            }

            // 3. Проверяем существующую регистрацию
            var existingReg = tournament.Registrations.FirstOrDefault(r => r.UserId == user.Id);
            if (existingReg != null)
            {
                if (existingReg.Status == RegStatus.Active)
                    return (false, "Вы уже записаны на этот турнир.");

                if (existingReg.Status == RegStatus.Played)
                    return (false, "Вы уже приняли участие в данном турнире.");

                // Статус был Canceled - реактивируем с проверкой мест
                var activeCountForReactivation = tournament.Registrations.Count(r => r.Status == RegStatus.Active);
                if (activeCountForReactivation >= tournament.MaxSeats)
                    return (false, "Свободных мест больше нет. Регистрация закрыта.");

                existingReg.Status = RegStatus.Active;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await SendRegistrationWebhookAsync(tournament, user);
                await SendVkConfirmationMessageAsync(tournament, user);

                return (true, "Запись успешно восстановлена! Ждем вас за столом.");
            }

            // 4. Проверка свободных мест для нового участника
            var activeCount = tournament.Registrations.Count(r => r.Status == RegStatus.Active);
            if (activeCount >= tournament.MaxSeats)
                return (false, "Свободных мест больше нет. Регистрация закрыта.");

            // 5. Записываем нового игрока
            var registration = new Registration
            {
                TournamentId = tournamentId,
                UserId = user.Id,
                Status = RegStatus.Active
            };

            _context.Registrations.Add(registration);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await SendRegistrationWebhookAsync(tournament, user);
            await SendVkConfirmationMessageAsync(tournament, user);

            return (true, "Вы успешно записаны на турнир! Ждем вас за столом.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Ошибка при регистрации на турнир: {ex.Message}");
        }
    }

    private async Task SendRegistrationWebhookAsync(Tournament tournament, User user)
    {
        var rawWebhookUrl = _configuration?["GoogleSheets:RegistrationWebhookUrl"]
            ?? _configuration?["GOOGLE_SHEETS_REGISTRATION_WEBHOOK_URL"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_SHEETS_REGISTRATION_WEBHOOK_URL");

        if (string.IsNullOrWhiteSpace(rawWebhookUrl))
        {
            return;
        }

        var webhookUrl = rawWebhookUrl.Trim();

        try
        {
            var rawTitle = !string.IsNullOrWhiteSpace(tournament.Title) ? tournament.Title.Trim() : "Турнир";
            var formattedDate = tournament.StartTime.TimeOfDay == TimeSpan.Zero
                ? tournament.StartTime.ToString("dd.MM.yyyy")
                : tournament.StartTime.ToString("dd.MM.yyyy HH:mm");
            var tournamentTitle = $"{rawTitle} ({formattedDate})";

            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            string playerName;
            if (!string.IsNullOrWhiteSpace(fullName) && !string.Equals(fullName, "Игрок VK", StringComparison.OrdinalIgnoreCase))
            {
                playerName = fullName;
            }
            else if (!string.IsNullOrWhiteSpace(user.Nickname))
            {
                playerName = user.Nickname.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(fullName))
            {
                playerName = fullName;
            }
            else
            {
                playerName = !string.IsNullOrWhiteSpace(user.VkId) ? $"Игрок {user.VkId}" : "Игрок";
            }

            var payload = new
            {
                tournamentTitle,
                playerName,
                clubCardId = user.ClubCardId?.Trim() ?? "",
                phoneNumber = user.PhoneNumber?.Trim() ?? "",
                source = "VK Mini App"
            };

            var client = GetHttpClient();
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = content
            };
            if (!request.Headers.UserAgent.Any())
            {
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) PokerClubApp/1.0");
            }

            var response = await client.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode &&
                response.StatusCode != System.Net.HttpStatusCode.Redirect &&
                response.StatusCode != System.Net.HttpStatusCode.Found)
            {
                _logger?.LogWarning(
                    "Вебхук Google Sheets вернул статус {StatusCode} для турнира {TournamentId}, пользователя {VkId}",
                    response.StatusCode, tournament.Id, user.VkId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Ошибка отправки вебхука регистрации Google Sheets для турнира {TournamentId}, пользователя {VkId}",
                tournament.Id, user.VkId);
        }
    }

    private async Task SendVkConfirmationMessageAsync(Tournament tournament, User user)
    {
        try
        {
            var communityToken = _configuration?["VkOptions:CommunityToken"]
                ?? _configuration?["VK_COMMUNITY_TOKEN"]
                ?? Environment.GetEnvironmentVariable("VK_COMMUNITY_TOKEN");

            if (string.IsNullOrWhiteSpace(communityToken))
            {
                _logger?.LogDebug("VK CommunityToken не настроен. Пропускаем отправку сообщения в ЛС.");
                return;
            }

            if (!long.TryParse(user.VkId, out var numericVkUserId) || numericVkUserId <= 0)
            {
                _logger?.LogDebug("User VkId '{VkId}' не является числовым идентификатором VK. Пропускаем отправку сообщения в ЛС.", user.VkId);
                return;
            }

            var clubAddress = !string.IsNullOrWhiteSpace(tournament.Club?.Address)
                ? tournament.Club.Address.Trim()
                : "Монастырская улица, 59, Пермь";

            var message = $"♠️ Вы успешно зарегистрированы на турнир \"{tournament.Title}\"!\n📅 Дата: {tournament.StartTime:dd.MM в HH:mm}\n📍 Адрес: {clubAddress}\n\nЖдем вас за столом клуба Monte Carlo!";

            var parameters = new Dictionary<string, string>
            {
                ["user_id"] = numericVkUserId.ToString(),
                ["random_id"] = Random.Shared.Next().ToString(),
                ["peer_id"] = numericVkUserId.ToString(),
                ["message"] = message,
                ["access_token"] = communityToken.Trim(),
                ["v"] = "5.199"
            };

            var client = GetHttpClient();
            using var content = new FormUrlEncodedContent(parameters);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await client.PostAsync("https://api.vk.com/method/messages.send", content, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || responseBody.Contains("\"error\":"))
            {
                _logger?.LogWarning("VK API messages.send вернул статус {StatusCode}: {Response}", response.StatusCode, responseBody);
            }
            else
            {
                _logger?.LogInformation("Успешно отправлено подтверждение в ЛС VK для пользователя {VkId} на турнир {TournamentId}", user.VkId, tournament.Id);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при отправке сообщения-подтверждения в VK для турнира {TournamentId}, пользователя {VkId}",
                tournament.Id, user.VkId);
        }
    }

    public async Task<(bool Success, string Message)> CancelRegistrationAsync(int tournamentId, string vkId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == vkId);
        if (user == null)
            return (false, "Пользователь не найден.");

        var tournament = await _context.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament == null)
            return (false, "Турнир не найден.");

        if (tournament.Status == TournamentStatus.Finished || tournament.Status == TournamentStatus.Canceled)
            return (false, "Невозможно отменить запись на завершенный или отмененный турнир.");

        var registration = tournament.Registrations.FirstOrDefault(r => r.UserId == user.Id);
        if (registration == null || registration.Status != RegStatus.Active)
            return (false, "Активная запись на данный турнир не найдена.");

        registration.Status = RegStatus.Canceled;
        await _context.SaveChangesAsync();

        return (true, "Запись на турнир успешно отменена.");
    }

    public async Task<(bool Success, Tournament? Tournament, string Message)> CreateTournamentAsync(
        int? clubId,
        string title,
        string? format,
        decimal buyIn,
        int maxSeats,
        DateTime startTime,
        string? description,
        int? cityId = null,
        string? address = null,
        DateTime? registrationEnd = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return (false, null, "Название турнира не может быть пустым.");

        Club? club = null;

        // 1. Если передан clubId > 0, ищем клуб по Id
        if (clubId.HasValue && clubId.Value > 0)
        {
            club = await _context.Clubs
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.Id == clubId.Value);
        }

        // 2. Если клуб не найден по Id, но передан адрес — ищем существующий клуб с таким адресом
        if (club == null && !string.IsNullOrWhiteSpace(address))
        {
            var trimmedAddress = address.Trim();
            var clubByAddressQuery = _context.Clubs.Include(c => c.City).AsQueryable();
            if (cityId.HasValue && cityId.Value > 0)
            {
                clubByAddressQuery = clubByAddressQuery.Where(c => c.CityId == cityId.Value);
            }
            club = await clubByAddressQuery.FirstOrDefaultAsync(c => c.Address == trimmedAddress);
        }

        // 3. Если клуб не найден по Id и адресу, но передан cityId, ищем клуб в этом городе
        if (club == null && cityId.HasValue && cityId.Value > 0)
        {
            club = await _context.Clubs
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.CityId == cityId.Value && c.IsActive)
                ?? await _context.Clubs
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.CityId == cityId.Value);

            // Если в указанном городе нет клубов, создаем клуб для этого города
            if (club == null)
            {
                var city = await _context.Cities.FirstOrDefaultAsync(c => c.Id == cityId.Value);
                if (city != null)
                {
                    club = new Club
                    {
                        CityId = city.Id,
                        Name = "Poker Club",
                        Address = !string.IsNullOrWhiteSpace(address) ? address.Trim() : $"г. {city.Name}",
                        IsActive = true,
                        City = city
                    };
                    _context.Clubs.Add(club);
                    await _context.SaveChangesAsync();
                }
            }
        }

        // 4. Если клуб все еще не найден, берем первый доступный клуб в базе
        if (club == null)
        {
            club = await _context.Clubs
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.IsActive)
                ?? await _context.Clubs
                .Include(c => c.City)
                .FirstOrDefaultAsync();
        }

        // 5. Если в базе вообще нет клубов, создаем дефолтный город и клуб
        if (club == null)
        {
            var city = (cityId.HasValue && cityId.Value > 0)
                ? await _context.Cities.FirstOrDefaultAsync(c => c.Id == cityId.Value)
                : await _context.Cities.FirstOrDefaultAsync();

            if (city == null)
            {
                city = new City
                {
                    Name = "Пермь",
                    Slug = "perm",
                    IsActive = true
                };
                _context.Cities.Add(city);
                await _context.SaveChangesAsync();
            }

            club = new Club
            {
                CityId = city.Id,
                Name = "Monte Carlo",
                Address = !string.IsNullOrWhiteSpace(address) ? address.Trim() : "Монастырская улица, 59, Пермь",
                IsActive = true,
                City = city
            };
            _context.Clubs.Add(club);
            await _context.SaveChangesAsync();
        }
        else if (!string.IsNullOrWhiteSpace(address) && club.Address != address.Trim())
        {
            // Обновляем адрес клуба, если из формы передан новый адрес
            club.Address = address.Trim();
            await _context.SaveChangesAsync();
        }

        var utcStartTime = startTime.Kind == DateTimeKind.Unspecified 
            ? DateTime.SpecifyKind(startTime, DateTimeKind.Utc) 
            : startTime.ToUniversalTime();

        DateTime? utcRegistrationEnd = null;
        if (registrationEnd.HasValue)
        {
            utcRegistrationEnd = registrationEnd.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(registrationEnd.Value, DateTimeKind.Utc)
                : registrationEnd.Value.ToUniversalTime();
        }

        var tournament = new Tournament
        {
            ClubId = club.Id,
            Title = title.Trim(),
            Format = string.IsNullOrWhiteSpace(format) ? "NL Holdem" : format.Trim(),
            BuyIn = Math.Max(0, buyIn),
            MaxSeats = maxSeats > 0 ? maxSeats : 30,
            StartTime = utcStartTime,
            RegistrationEnd = utcRegistrationEnd,
            Description = description?.Trim(),
            Status = TournamentStatus.RegistrationOpen,
            CreatedAt = DateTime.UtcNow,
            Club = club
        };

        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();

        return (true, tournament, "Турнир успешно создан!");
    }

    public async Task<(bool Success, string Message)> DeleteTournamentAsync(int id)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null)
            return (false, "Турнир не найден.");

        _context.Registrations.RemoveRange(tournament.Registrations);
        _context.Tournaments.Remove(tournament);
        await _context.SaveChangesAsync();

        return (true, "Турнир успешно удален.");
    }
}