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
    public const string DefaultCsvUrl = "https://docs.google.com/spreadsheets/d/1GRINVjwfqXsG0vccHfFFOaxzTbo5pcxWBGn1YOgzOn0/gviz/tq?tqx=out:csv&sheet=%D0%9E%D0%B1%D1%89%D0%B8%D0%B9%20%D1%80%D0%B5%D0%B9%D1%82%D0%B8%D0%BD%D0%B3";
    public const string RegistrationsCsvUrl = "https://docs.google.com/spreadsheets/d/1GRINVjwfqXsG0vccHfFFOaxzTbo5pcxWBGn1YOgzOn0/gviz/tq?tqx=out:csv&sheet=%D0%A0%D0%95%D0%93%D0%98%D0%A1%D0%A2%D0%A0%D0%90%D0%A6%D0%98%D0%98";

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
            var ratingResponse = await _httpClient.GetAsync(DefaultCsvUrl, cancellationToken);
            if (!ratingResponse.IsSuccessStatusCode)
            {
                var errMsg = $"Google Sheets вернул статус {ratingResponse.StatusCode}: {ratingResponse.ReasonPhrase}";
                _logger.LogError("{ErrorMessage}", errMsg);
                return new GoogleSheetsSyncResult(false, 0, 0, 0, errMsg);
            }

            var ratingCsvContent = await ratingResponse.Content.ReadAsStringAsync(cancellationToken);

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
                _logger.LogWarning(ex, "Не удалось загрузить лист РЕГИСТРАЦИИ, продолжаем синхронизацию только по рейтингу.");
            }

            return await SyncFromCsvAsync(ratingCsvContent, registrationsCsvContent, cancellationToken);
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

    public async Task<GoogleSheetsSyncResult> SyncFromCsvAsync(
        string ratingCsvContent, 
        string? registrationsCsvContent, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ratingCsvContent))
        {
            return new GoogleSheetsSyncResult(false, 0, 0, 0, "CSV контент пуст.");
        }

        var parsedRows = ParseCsv(ratingCsvContent);
        if (parsedRows.Count == 0)
        {
            return new GoogleSheetsSyncResult(false, 0, 0, 0, "Не удалось распознать строки в CSV.");
        }

        // Парсим сопоставление имени к реальному номеру карты (ClubCardId) и телефону из листа РЕГИСТРАЦИИ
        var regMap = ParseRegistrationsMapping(registrationsCsvContent ?? "");

        var existingUsers = await _context.Users.ToListAsync(cancellationToken);

        int totalProcessed = 0;
        int updatedCount = 0;
        int createdCount = 0;

        foreach (var row in parsedRows)
        {
            if (row.Count < 2) continue;

            // Пропускаем строку заголовков
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

            totalProcessed++;

            // Ищем привязанный клубный ID и телефон из листа РЕГИСТРАЦИИ
            var normKey = NormalizeNameKey(playerName);
            string? knownCardId = null;
            string? knownPhone = null;

            if (regMap.TryGetValue(normKey, out var regInfo))
            {
                knownCardId = regInfo.ClubCardId;
                knownPhone = regInfo.PhoneNumber;
            }

            // Поиск совпадения в БД по реальному ClubCardId или ФИО
            var matchedUser = FindMatchingUser(existingUsers, playerName, knownCardId);

            if (matchedUser != null)
            {
                matchedUser.TotalRating = points;
                matchedUser.TournamentsPlayed = tournaments;
                matchedUser.WinsCount = wins;
                matchedUser.Top3Count = top3;
                matchedUser.Top10Count = top10;
                matchedUser.KnockoutsCount = knockouts;
                matchedUser.AvgPlace = avgPlace;

                if (!string.IsNullOrWhiteSpace(knownCardId))
                {
                    matchedUser.ClubCardId = knownCardId;
                }

                if (string.IsNullOrWhiteSpace(matchedUser.PhoneNumber) && !string.IsNullOrWhiteSpace(knownPhone))
                {
                    matchedUser.PhoneNumber = knownPhone;
                }

                updatedCount++;
            }
            else
            {
                var nameParts = playerName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string lastName = nameParts.Length > 0 ? nameParts[0] : playerName;
                string firstName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "Игрок";

                var uid = Guid.NewGuid().ToString("N")[..8];
                var newVkId = $"sheet_{place}_{uid}";
                if (newVkId.Length > 50) newVkId = newVkId[..50];

                var newUser = new User
                {
                    VkId = newVkId,
                    FirstName = firstName,
                    LastName = lastName,
                    TotalRating = points,
                    TournamentsPlayed = tournaments,
                    WinsCount = wins,
                    Top3Count = top3,
                    Top10Count = top10,
                    KnockoutsCount = knockouts,
                    AvgPlace = avgPlace,
                    ClubCardId = knownCardId,
                    PhoneNumber = knownPhone,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                existingUsers.Add(newUser);
                createdCount++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

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
