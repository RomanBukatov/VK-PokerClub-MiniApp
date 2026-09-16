using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;

namespace PokerClub.Infrastructure.Services;

public class GoogleSheetsSyncService : IGoogleSheetsSyncService
{
    public const string SeasonRatingCsvUrl = "https://docs.google.com/spreadsheets/d/1GRINVjwfqXsG0vccHfFFOaxzTbo5pcxWBGn1YOgzOn0/gviz/tq?tqx=out:csv&sheet=%D0%A0%D0%B5%D0%B9%D1%82%D0%B8%D0%BD%D0%B3%20%D1%81%D0%B5%D0%B7%D0%BE%D0%BD%D0%B0";
    public const string TotalRatingCsvUrl = "https://docs.google.com/spreadsheets/d/1GRINVjwfqXsG0vccHfFFOaxzTbo5pcxWBGn1YOgzOn0/gviz/tq?tqx=out:csv&sheet=%D0%9E%D0%B1%D1%89%D0%B8%D0%B9%20%D1%80%D0%B5%D0%B9%D1%82%D0%B8%D0%BD%D0%B3";
    public const string RegistrationsCsvUrl = "https://docs.google.com/spreadsheets/d/1GRINVjwfqXsG0vccHfFFOaxzTbo5pcxWBGn1YOgzOn0/gviz/tq?tqx=out:csv&sheet=%D0%A0%D0%95%D0%93%D0%98%D0%A1%D0%A2%D0%A0%D0%90%D0%A6%D0%98%D0%98";
    public const string DefaultCsvUrl = TotalRatingCsvUrl;

    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleSheetsSyncService> _logger;

    public GoogleSheetsSyncService(
        AppDbContext context,
        HttpClient httpClient,
        ILogger<GoogleSheetsSyncService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GoogleSheetsSyncResult> SyncFromGoogleSheetsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Запуск загрузки данных рейтинга из Google Sheets...");

            string? seasonCsvContent = null;
            try
            {
                var seasonResponse = await _httpClient.GetAsync(SeasonRatingCsvUrl, cancellationToken);
                if (seasonResponse.IsSuccessStatusCode)
                {
                    seasonCsvContent = await seasonResponse.Content.ReadAsStringAsync(cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Лист «Рейтинг сезона» вернул статус {StatusCode}", seasonResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить лист «Рейтинг сезона»");
            }

            string? totalCsvContent = null;
            try
            {
                var totalResponse = await _httpClient.GetAsync(TotalRatingCsvUrl, cancellationToken);
                if (totalResponse.IsSuccessStatusCode)
                {
                    totalCsvContent = await totalResponse.Content.ReadAsStringAsync(cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Лист «Общий рейтинг» вернул статус {StatusCode}", totalResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить лист «Общий рейтинг»");
            }

            if (string.IsNullOrWhiteSpace(seasonCsvContent) && string.IsNullOrWhiteSpace(totalCsvContent))
            {
                var errMsg = "Не удалось загрузить ни лист «Рейтинг сезона», ни «Общий рейтинг».";
                _logger.LogError("{ErrorMessage}", errMsg);
                return new GoogleSheetsSyncResult(false, 0, 0, 0, errMsg);
            }

            string? registrationsCsvContent = null;
            try
            {
                var regResponse = await _httpClient.GetAsync(RegistrationsCsvUrl, cancellationToken);
                if (regResponse.IsSuccessStatusCode)
                {
                    registrationsCsvContent = await regResponse.Content.ReadAsStringAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить лист «РЕГИСТРАЦИИ», продолжаем синхронизацию только по рейтингу.");
            }

            return await SyncFromCsvAsync(seasonCsvContent, totalCsvContent, registrationsCsvContent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при синхронизации с Google Sheets");
            return new GoogleSheetsSyncResult(false, 0, 0, 0, $"Ошибка при синхронизации: {ex.Message}");
        }
    }

    public Task<GoogleSheetsSyncResult> SyncFromCsvAsync(string csvContent, CancellationToken cancellationToken = default)
    {
        return SyncFromCsvAsync(csvContent, null, cancellationToken);
    }

    public Task<GoogleSheetsSyncResult> SyncFromCsvAsync(
        string ratingCsvContent, 
        string? registrationsCsvContent, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ratingCsvContent))
        {
            return Task.FromResult(new GoogleSheetsSyncResult(false, 0, 0, 0, "CSV контент пуст."));
        }

        bool isSeasonOnly = ratingCsvContent.Contains("сезон", StringComparison.OrdinalIgnoreCase) && 
                            !ratingCsvContent.Contains("общий", StringComparison.OrdinalIgnoreCase);

        if (isSeasonOnly)
        {
            return SyncFromCsvAsync(ratingCsvContent, null, registrationsCsvContent, cancellationToken);
        }
        else
        {
            return SyncFromCsvAsync(null, ratingCsvContent, registrationsCsvContent, cancellationToken);
        }
    }

    public record SheetPlayerStats(
        int Place,
        string PlayerName,
        int TournamentsPlayed,
        int WinsCount,
        int Top3Count,
        int Top10Count,
        int KnockoutsCount,
        int Points,
        double AvgPlace
    );

    public static List<SheetPlayerStats> ParseRatingSheet(string csvContent)
    {
        var result = new List<SheetPlayerStats>();
        if (string.IsNullOrWhiteSpace(csvContent)) return result;

        var parsedRows = ParseCsv(csvContent);
        foreach (var row in parsedRows)
        {
            if (row.Count < 2) continue;

            var col0 = row[0].Trim();
            var col1 = row[1].Trim();
            if (col0.Contains("место", StringComparison.OrdinalIgnoreCase) ||
                col1.Contains("игрок", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(col0, out var place))
            {
                continue;
            }

            var playerName = col1;
            if (string.IsNullOrWhiteSpace(playerName)) continue;

            int tournaments = row.Count > 2 ? ParseIntClean(row[2]) : 0;
            int wins = row.Count > 3 ? ParseIntClean(row[3]) : 0;
            int top3 = row.Count > 4 ? ParseIntClean(row[4]) : 0;
            int top10 = row.Count > 5 ? ParseIntClean(row[5]) : 0;
            int knockouts = row.Count > 6 ? ParseIntClean(row[6]) : 0;
            int points = row.Count > 7 ? ParseIntClean(row[7]) : 0;
            double avgPlace = row.Count > 8 ? ParseDoubleClean(row[8]) : 0.0;

            result.Add(new SheetPlayerStats(
                place,
                playerName,
                tournaments,
                wins,
                top3,
                top10,
                knockouts,
                points,
                avgPlace
            ));
        }

        return result;
    }

    public async Task<GoogleSheetsSyncResult> SyncFromCsvAsync(
        string? seasonRatingCsvContent,
        string? totalRatingCsvContent,
        string? registrationsCsvContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(seasonRatingCsvContent) && string.IsNullOrWhiteSpace(totalRatingCsvContent))
        {
            return new GoogleSheetsSyncResult(false, 0, 0, 0, "CSV контент пуст.");
        }

        var seasonRows = ParseRatingSheet(seasonRatingCsvContent ?? "");
        var totalRows = ParseRatingSheet(totalRatingCsvContent ?? "");

        if (seasonRows.Count == 0 && totalRows.Count == 0)
        {
            return new GoogleSheetsSyncResult(false, 0, 0, 0, "Не удалось распознать строки в CSV.");
        }

        var regMap = ParseRegistrationsMapping(registrationsCsvContent ?? "");
        var existingUsers = await _context.Users.ToListAsync(cancellationToken);

        var processedUsers = new HashSet<User>();
        var updatedUsers = new HashSet<User>();
        var createdUsers = new HashSet<User>();

        // 1. Обработка Общего рейтинга (пишет в TotalRating)
        foreach (var row in totalRows)
        {
            var normKey = NormalizeNameKey(row.PlayerName);
            string? knownCardId = null;
            string? knownPhone = null;

            if (regMap.TryGetValue(normKey, out var regInfo))
            {
                knownCardId = regInfo.ClubCardId;
                knownPhone = regInfo.PhoneNumber;
            }

            var matchedUser = FindMatchingUser(existingUsers, row.PlayerName, knownCardId);
            if (matchedUser != null)
            {
                matchedUser.TotalRating = row.Points;
                matchedUser.TournamentsPlayed = row.TournamentsPlayed;
                matchedUser.WinsCount = row.WinsCount;
                matchedUser.Top3Count = row.Top3Count;
                matchedUser.Top10Count = row.Top10Count;
                matchedUser.KnockoutsCount = row.KnockoutsCount;
                matchedUser.AvgPlace = row.AvgPlace;

                if (!string.IsNullOrWhiteSpace(knownCardId))
                {
                    matchedUser.ClubCardId = knownCardId;
                }

                if (string.IsNullOrWhiteSpace(matchedUser.PhoneNumber) && !string.IsNullOrWhiteSpace(knownPhone))
                {
                    matchedUser.PhoneNumber = knownPhone;
                }

                processedUsers.Add(matchedUser);
                updatedUsers.Add(matchedUser);
            }
            else
            {
                var nameParts = row.PlayerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string lastName = nameParts.Length > 0 ? nameParts[0] : row.PlayerName;
                string firstName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "Игрок";

                var uid = Guid.NewGuid().ToString("N")[..8];
                var newVkId = $"sheet_{row.Place}_{uid}";
                if (newVkId.Length > 50) newVkId = newVkId[..50];

                var newUser = new User
                {
                    VkId = newVkId,
                    FirstName = firstName,
                    LastName = lastName,
                    TotalRating = row.Points,
                    SeasonRating = 0,
                    TournamentsPlayed = row.TournamentsPlayed,
                    WinsCount = row.WinsCount,
                    Top3Count = row.Top3Count,
                    Top10Count = row.Top10Count,
                    KnockoutsCount = row.KnockoutsCount,
                    AvgPlace = row.AvgPlace,
                    ClubCardId = knownCardId,
                    PhoneNumber = knownPhone,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                existingUsers.Add(newUser);
                processedUsers.Add(newUser);
                createdUsers.Add(newUser);
            }
        }

        // 2. Обработка Рейтинга сезона (пишет в SeasonRating)
        foreach (var row in seasonRows)
        {
            var normKey = NormalizeNameKey(row.PlayerName);
            string? knownCardId = null;
            string? knownPhone = null;

            if (regMap.TryGetValue(normKey, out var regInfo))
            {
                knownCardId = regInfo.ClubCardId;
                knownPhone = regInfo.PhoneNumber;
            }

            var matchedUser = FindMatchingUser(existingUsers, row.PlayerName, knownCardId);
            if (matchedUser != null)
            {
                matchedUser.SeasonRating = row.Points;

                // Если общий рейтинг не загружался или у пользователя еще не заполнены турниры
                if (totalRows.Count == 0 || matchedUser.TournamentsPlayed == 0)
                {
                    matchedUser.TournamentsPlayed = row.TournamentsPlayed;
                    matchedUser.WinsCount = row.WinsCount;
                    matchedUser.Top3Count = row.Top3Count;
                    matchedUser.Top10Count = row.Top10Count;
                    matchedUser.KnockoutsCount = row.KnockoutsCount;
                    matchedUser.AvgPlace = row.AvgPlace;
                }

                if (!string.IsNullOrWhiteSpace(knownCardId) && string.IsNullOrWhiteSpace(matchedUser.ClubCardId))
                {
                    matchedUser.ClubCardId = knownCardId;
                }

                if (string.IsNullOrWhiteSpace(matchedUser.PhoneNumber) && !string.IsNullOrWhiteSpace(knownPhone))
                {
                    matchedUser.PhoneNumber = knownPhone;
                }

                processedUsers.Add(matchedUser);
                if (!createdUsers.Contains(matchedUser))
                {
                    updatedUsers.Add(matchedUser);
                }
            }
            else
            {
                var nameParts = row.PlayerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string lastName = nameParts.Length > 0 ? nameParts[0] : row.PlayerName;
                string firstName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "Игрок";

                var uid = Guid.NewGuid().ToString("N")[..8];
                var newVkId = $"sheet_{row.Place}_{uid}";
                if (newVkId.Length > 50) newVkId = newVkId[..50];

                var newUser = new User
                {
                    VkId = newVkId,
                    FirstName = firstName,
                    LastName = lastName,
                    SeasonRating = row.Points,
                    TotalRating = 0,
                    TournamentsPlayed = row.TournamentsPlayed,
                    WinsCount = row.WinsCount,
                    Top3Count = row.Top3Count,
                    Top10Count = row.Top10Count,
                    KnockoutsCount = row.KnockoutsCount,
                    AvgPlace = row.AvgPlace,
                    ClubCardId = knownCardId,
                    PhoneNumber = knownPhone,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                existingUsers.Add(newUser);
                processedUsers.Add(newUser);
                createdUsers.Add(newUser);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        int totalProcessed = processedUsers.Count;
        int createdCount = createdUsers.Count;
        int updatedCount = updatedUsers.Count;

        var message = $"Синхронизация Google Sheets завершена: обработано {totalProcessed}, обновлено {updatedCount}, создано {createdCount}.";
        _logger.LogInformation("{SyncMessage}", message);

        return new GoogleSheetsSyncResult(true, totalProcessed, updatedCount, createdCount, message);
    }

    public static User? FindMatchingUser(
        IEnumerable<User> users, 
        string playerName, 
        string? clubCardId = null)
    {
        // 1. Точное сопоставление по реальному ClubCardId (если передан)
        if (!string.IsNullOrWhiteSpace(clubCardId))
        {
            var trimmedCard = clubCardId.Trim();
            var byCard = users.FirstOrDefault(u => 
                !string.IsNullOrWhiteSpace(u.ClubCardId) && 
                string.Equals(u.ClubCardId.Trim(), trimmedCard, StringComparison.OrdinalIgnoreCase));
            if (byCard != null) return byCard;
        }

        // 2. Сопоставление по токенам ФИО
        var playerTokens = TokenizeName(playerName);
        if (playerTokens.Count == 0) return null;

        foreach (var u in users)
        {
            var userFullName = $"{u.FirstName} {u.LastName}".Trim();
            var userTokens = TokenizeName(userFullName);
            if (TokensMatch(playerTokens, userTokens))
            {
                return u;
            }

            // 3. Сопоставление по Никнейму
            if (!string.IsNullOrWhiteSpace(u.Nickname))
            {
                var nickTokens = TokenizeName(u.Nickname);
                if (TokensMatch(playerTokens, nickTokens))
                {
                    return u;
                }
            }
        }

        return null;
    }

    public static bool TokensMatch(HashSet<string> t1, HashSet<string> t2)
    {
        if (t1.Count == 0 || t2.Count == 0) return false;

        // Если множества полностью совпадают (например, {"лукашенко", "василий"} == {"василий", "лукашенко"})
        if (t1.SetEquals(t2)) return true;

        // Пересечение токенов
        var commonCount = t1.Intersect(t2).Count();

        // Если совпадает 2 или более токенов (например, Фамилия + Имя при наличии Отчества)
        if (commonCount >= 2) return true;

        // Исключаем ложные совпадения по одиночным именам ("Александр", "Иван", "Игрок")
        return false;
    }

    public record PlayerCardInfo(string ClubCardId, string? PhoneNumber, string FullName);

    public static Dictionary<string, PlayerCardInfo> ParseRegistrationsMapping(string registrationsCsv)
    {
        var mapping = new Dictionary<string, PlayerCardInfo>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(registrationsCsv)) return mapping;

        var rows = ParseCsv(registrationsCsv);
        foreach (var row in rows)
        {
            if (row.Count < 3) continue;

            var col0 = row[0].Trim();
            var col1 = row[1].Trim(); // ИМЯ
            var col2 = row[2].Trim(); // ID (ClubCardId)
            var col3 = row.Count > 3 ? row[3].Trim() : null; // ТЕЛЕФОН

            if (col0.Contains("турнир", StringComparison.OrdinalIgnoreCase) ||
                col1.Contains("имя", StringComparison.OrdinalIgnoreCase) ||
                col2.Contains("id", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(col1) || string.IsNullOrWhiteSpace(col2))
                continue;

            var cardId = col2.Trim();
            var playerName = col1.Trim();
            var phone = !string.IsNullOrWhiteSpace(col3) ? col3 : null;

            var info = new PlayerCardInfo(cardId, phone, playerName);

            var normKey = NormalizeNameKey(playerName);
            if (!string.IsNullOrEmpty(normKey) && !mapping.ContainsKey(normKey))
            {
                mapping[normKey] = info;
            }

            var cardKey = $"card_{cardId}";
            if (!mapping.ContainsKey(cardKey))
            {
                mapping[cardKey] = info;
            }
        }

        return mapping;
    }

    public static string NormalizeNameKey(string name)
    {
        var tokens = TokenizeName(name).OrderBy(x => x, StringComparer.Ordinal);
        return string.Join("_", tokens);
    }

    public static HashSet<string> TokenizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var normalized = name.ToLowerInvariant()
            .Replace('ё', 'е');

        var parts = normalized.Split(new[] { ' ', '.', ',', '-', '_', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return new HashSet<string>(parts.Where(p => p.Length > 1), StringComparer.OrdinalIgnoreCase);
    }

    public static int ParseIntClean(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return 0;
        var clean = str.Trim().Replace(" ", "").Replace("\u00A0", "");
        return int.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) ? val : 0;
    }

    public static double ParseDoubleClean(string? str)
    {
        if (string.IsNullOrWhiteSpace(str)) return 0.0;
        var clean = str.Trim().Replace(',', '.').Replace(" ", "").Replace("\u00A0", "");
        return double.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0.0;
    }

    public static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(csv))
            return rows;

        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    currentCell.Append('"');
                    i++; // Пропускаем экранированную кавычку
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                currentRow.Add(currentCell.ToString().Trim());
                currentCell.Clear();
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(currentCell.ToString().Trim());
                currentCell.Clear();

                if (currentRow.Any(cell => !string.IsNullOrEmpty(cell)))
                {
                    rows.Add(currentRow);
                }
                currentRow = new List<string>();
            }
            else
            {
                currentCell.Append(c);
            }
        }

        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString().Trim());
            if (currentRow.Any(cell => !string.IsNullOrEmpty(cell)))
            {
                rows.Add(currentRow);
            }
        }

        return rows;
    }
}
