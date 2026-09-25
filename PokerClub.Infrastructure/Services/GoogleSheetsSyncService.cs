using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokerClub.Domain.Constants;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;

namespace PokerClub.Infrastructure.Services;

public class GoogleSheetsSyncService : IGoogleSheetsSyncService
{
    public const string DefaultSpreadsheetId = "1zxU_LOSjIsjrEHw7eq366BQMLWQ4x8pSDIiMdUxWPOs";
    public const string AutumnSeasonSheetName = "Осенний сезон 2026";
    public const string LegacySeasonRatingSheetName = "Рейтинг сезона";
    public const string SeasonRatingSheetName = "Рейтинг сезона";
    public const string DefaultSeasonGid = "202622";
    public const string SeasonRatingGid = "646289371";
    public const string TotalRatingSheetName = "Общий рейтинг";
    public const string TotalRatingGid = "0";
    public const string RegistrationsSheetName = "РЕГИСТРАЦИИ";
    public const string? RegistrationsGid = null;
    public const string PlayersSheetName = "Игроки";
    public const string? PlayersGid = null;
    public const string DefaultSeasonName = "Осень 2026";

    private static string _activeSeasonName = DefaultSeasonName;

    public static string CurrentSeasonName
    {
        get => _activeSeasonName;
        set => _activeSeasonName = string.IsNullOrWhiteSpace(value) ? DefaultSeasonName : value.Trim();
    }

    public string GetActiveSeasonName() => CurrentSeasonName;

    public const string SeasonRatingCsvUrl = $"https://docs.google.com/spreadsheets/d/{DefaultSpreadsheetId}/gviz/tq?tqx=out:csv&sheet=%D0%9E%D1%81%D0%B5%D0%BD%D0%BD%D0%B8%D0%B9%20%D1%81%D0%B5%D0%B7%D0%BE%D0%BD%202026";
    public const string TotalRatingCsvUrl = $"https://docs.google.com/spreadsheets/d/{DefaultSpreadsheetId}/gviz/tq?tqx=out:csv&sheet=%D0%9E%D0%B1%D1%89%D0%B8%D0%B9%20%D1%80%D0%B5%D0%B9%D1%82%D0%B8%D0%BD%D0%B3";
    public const string RegistrationsCsvUrl = $"https://docs.google.com/spreadsheets/d/{DefaultSpreadsheetId}/gviz/tq?tqx=out:csv&sheet=%D0%A0%D0%95%D0%93%D0%98%D0%A1%D0%A2%D0%A0%D0%90%D0%A6%D0%98%D0%98";
    public const string PlayersCsvUrl = $"https://docs.google.com/spreadsheets/d/{DefaultSpreadsheetId}/gviz/tq?tqx=out:csv&sheet=%D0%98%D0%B3%D1%80%D0%BE%D0%BA%D0%B8";
    public const string DefaultCsvUrl = TotalRatingCsvUrl;

    private readonly AppDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleSheetsSyncService> _logger;
    private readonly string _spreadsheetId;
    private readonly string _seasonGid;
    private readonly string _seasonSheetName;
    private readonly string? _registrationsGid;
    private readonly string _playersSheetName;
    private readonly string? _playersGid;
    private readonly ILeaderboardCacheResetToken? _cacheResetToken;

    public GoogleSheetsSyncService(
        AppDbContext context,
        HttpClient httpClient,
        ILogger<GoogleSheetsSyncService> logger)
        : this(context, httpClient, logger, (IConfiguration?)null)
    {
    }

    [ActivatorUtilitiesConstructor]
    public GoogleSheetsSyncService(
        AppDbContext context,
        HttpClient httpClient,
        ILogger<GoogleSheetsSyncService> logger,
        IConfiguration? configuration,
        ILeaderboardCacheResetToken? cacheResetToken = null)
        : this(
            context,
            httpClient,
            logger,
            configuration?["GoogleSheets:SpreadsheetId"]
                ?? configuration?["GOOGLE_SHEETS_SPREADSHEET_ID"]
                ?? configuration?["SPREADSHEET_ID"]
                ?? configuration?["SpreadsheetId"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_SHEETS_SPREADSHEET_ID")
                ?? DefaultSpreadsheetId,
            configuration?["GoogleSheets:SeasonGid"]
                ?? configuration?["GOOGLE_SHEETS_SEASON_GID"]
                ?? configuration?["SEASON_GID"]
                ?? configuration?["SeasonGid"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_SHEETS_SEASON_GID")
                ?? DefaultSeasonGid,
            configuration?["GoogleSheets:SeasonSheetName"]
                ?? configuration?["GOOGLE_SHEETS_SEASON_SHEET_NAME"]
                ?? configuration?["SEASON_SHEET_NAME"]
                ?? AutumnSeasonSheetName,
            configuration?["GoogleSheets:RegistrationsGid"]
                ?? configuration?["REGISTRATIONS_GID"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_SHEETS_REGISTRATIONS_GID"),
            configuration?["GoogleSheets:PlayersSheetName"]
                ?? configuration?["GOOGLE_SHEETS_PLAYERS_SHEET_NAME"]
                ?? configuration?["PLAYERS_SHEET_NAME"]
                ?? PlayersSheetName,
            configuration?["GoogleSheets:PlayersGid"]
                ?? configuration?["GOOGLE_SHEETS_PLAYERS_GID"]
                ?? configuration?["PLAYERS_GID"]
                ?? Environment.GetEnvironmentVariable("GOOGLE_SHEETS_PLAYERS_GID"),
            cacheResetToken)
    {
    }

    public GoogleSheetsSyncService(
        AppDbContext context,
        HttpClient httpClient,
        ILogger<GoogleSheetsSyncService> logger,
        string spreadsheetId,
        string? registrationsGid = null,
        ILeaderboardCacheResetToken? cacheResetToken = null)
        : this(context, httpClient, logger, spreadsheetId, DefaultSeasonGid, AutumnSeasonSheetName, registrationsGid, PlayersSheetName, null, cacheResetToken)
    {
    }

    public GoogleSheetsSyncService(
        AppDbContext context,
        HttpClient httpClient,
        ILogger<GoogleSheetsSyncService> logger,
        string spreadsheetId,
        string seasonGid,
        string seasonSheetName,
        string? registrationsGid = null,
        ILeaderboardCacheResetToken? cacheResetToken = null)
        : this(context, httpClient, logger, spreadsheetId, seasonGid, seasonSheetName, registrationsGid, PlayersSheetName, null, cacheResetToken)
    {
    }

    public GoogleSheetsSyncService(
        AppDbContext context,
        HttpClient httpClient,
        ILogger<GoogleSheetsSyncService> logger,
        string spreadsheetId,
        string seasonGid,
        string seasonSheetName,
        string? registrationsGid,
        string? playersSheetName,
        string? playersGid = null,
        ILeaderboardCacheResetToken? cacheResetToken = null)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
        _spreadsheetId = string.IsNullOrWhiteSpace(spreadsheetId) ? DefaultSpreadsheetId : spreadsheetId;
        _seasonGid = string.IsNullOrWhiteSpace(seasonGid) ? DefaultSeasonGid : seasonGid;
        _seasonSheetName = string.IsNullOrWhiteSpace(seasonSheetName) ? AutumnSeasonSheetName : seasonSheetName;
        _registrationsGid = registrationsGid;
        _playersSheetName = string.IsNullOrWhiteSpace(playersSheetName) ? PlayersSheetName : playersSheetName;
        _playersGid = playersGid;
        _cacheResetToken = cacheResetToken;

        try
        {
            if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            }
        }
        catch
        {
            // Ignore if headers are read-only or locked
        }
    }

    private static readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);

    public Task<GoogleSheetsSyncResult> SyncAsync(CancellationToken cancellationToken = default)
        => SyncFromGoogleSheetsAsync(cancellationToken);

    public async Task<GoogleSheetsSyncResult> SyncFromGoogleSheetsAsync(CancellationToken cancellationToken = default)
    {
        if (!await _syncLock.WaitAsync(0))
        {
            _logger.LogWarning("Синхронизация уже выполняется в фоновом режиме.");
            return new SyncResultDto(0, 0, 0, "Синхронизация уже выполняется в фоновом режиме", true);
        }

        try
        {
            _logger.LogInformation("Запуск загрузки данных рейтинга из Google Sheets...");

            var seasonCsvContent = await DownloadCsvWithFallbackAsync(_seasonSheetName, _seasonGid, cancellationToken);
            if (string.IsNullOrWhiteSpace(seasonCsvContent) && !string.Equals(_seasonGid, SeasonRatingGid, StringComparison.OrdinalIgnoreCase))
            {
                // Резервная попытка загрузить по legacy имени / gid, если активный сезон не вернул данных
                seasonCsvContent = await DownloadCsvWithFallbackAsync(LegacySeasonRatingSheetName, SeasonRatingGid, cancellationToken);
            }

            var totalCsvContent = await DownloadCsvWithFallbackAsync(TotalRatingSheetName, TotalRatingGid, cancellationToken);

            if (string.IsNullOrWhiteSpace(seasonCsvContent) && string.IsNullOrWhiteSpace(totalCsvContent))
            {
                var errMsg = "Не удалось загрузить ни лист «Рейтинг сезона», ни «Общий рейтинг».";
                _logger.LogError("{ErrorMessage}", errMsg);
                return new GoogleSheetsSyncResult(false, 0, 0, 0, errMsg);
            }

            string? registrationsCsvContent = null;
            try
            {
                registrationsCsvContent = await DownloadCsvWithFallbackAsync(RegistrationsSheetName, _registrationsGid, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить лист «{RegistrationsSheetName}», продолжаем синхронизацию только по рейтингу.", RegistrationsSheetName);
            }

            if (string.IsNullOrWhiteSpace(registrationsCsvContent))
            {
                _logger.LogWarning("Лист «{RegistrationsSheetName}» не был загружен или пуст, продолжаем синхронизацию только по рейтингу.", RegistrationsSheetName);
            }

            string? playersCsvContent = null;
            try
            {
                playersCsvContent = await DownloadCsvWithFallbackAsync(_playersSheetName, _playersGid, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить лист «{PlayersSheetName}», продолжаем синхронизацию без листа игроков.", _playersSheetName);
            }

            if (string.IsNullOrWhiteSpace(playersCsvContent))
            {
                _logger.LogWarning("Лист «{PlayersSheetName}» не был загружен или пуст, продолжаем синхронизацию без листа игроков.", _playersSheetName);
            }

            return await SyncFromCsvAsync(seasonCsvContent, totalCsvContent, registrationsCsvContent, playersCsvContent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при синхронизации с Google Sheets");
            return new GoogleSheetsSyncResult(false, 0, 0, 0, $"Ошибка при синхронизации: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<string?> DownloadCsvWithFallbackAsync(
        string sheetName, 
        string? gid = null, 
        CancellationToken cancellationToken = default)
    {
        // Auto-resolve known gids if not explicitly provided
        if (string.IsNullOrWhiteSpace(gid))
        {
            if (string.Equals(sheetName, AutumnSeasonSheetName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sheetName, "Осень 2026", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sheetName, _seasonSheetName, StringComparison.OrdinalIgnoreCase))
            {
                gid = _seasonGid;
            }
            else if (string.Equals(sheetName, SeasonRatingSheetName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(sheetName, LegacySeasonRatingSheetName, StringComparison.OrdinalIgnoreCase))
            {
                gid = SeasonRatingGid;
            }
            else if (string.Equals(sheetName, TotalRatingSheetName, StringComparison.OrdinalIgnoreCase))
            {
                gid = TotalRatingGid;
            }
            else if (string.Equals(sheetName, RegistrationsSheetName, StringComparison.OrdinalIgnoreCase))
            {
                gid = _registrationsGid ?? RegistrationsGid;
            }
            else if (string.Equals(sheetName, PlayersSheetName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(sheetName, _playersSheetName, StringComparison.OrdinalIgnoreCase))
            {
                gid = _playersGid ?? PlayersGid;
            }
        }

        var gvizUrl = !string.IsNullOrWhiteSpace(sheetName)
            ? $"https://docs.google.com/spreadsheets/d/{_spreadsheetId}/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(sheetName)}"
            : $"https://docs.google.com/spreadsheets/d/{_spreadsheetId}/gviz/tq?tqx=out:csv&gid={gid}";

        try
        {
            _logger.LogInformation("Попытка загрузки листа «{SheetName}» через gviz API. URL: {Url}", sheetName, gvizUrl);
            var response = await _httpClient.GetAsync(gvizUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (IsValidCsvContent(content))
                {
                    if (!IsExpectedRatingSheet(sheetName) && (IsRatingSheetCsv(content) || content.Contains("ОБЩИЙ РЕЙТИНГ", StringComparison.OrdinalIgnoreCase)))
                    {
                        _logger.LogWarning("Лист «{SheetName}» вернул содержимое общего рейтинга вместо запрошенного листа (gviz вернул gid=0). Считаем лист не найденным.", sheetName);
                    }
                    else if (IsPlayersSheetName(sheetName) && !IsPlayersSheetCsv(content))
                    {
                        _logger.LogWarning("Лист «{SheetName}» вернул другой лист (проверка заголовков не совпадает с листом игроков). Считаем лист не найденным.", sheetName);
                    }
                    else
                    {
                        _logger.LogInformation("Лист «{SheetName}» успешно загружен через gviz API.", sheetName);
                        return content;
                    }
                }
                else
                {
                    _logger.LogError(
                        "Лист «{SheetName}» вернул HTTP {StatusCode}, но содержимое не является валидным CSV. URL: {Url}, Ответ: {ResponseBody}",
                        sheetName, $"{(int)response.StatusCode} ({response.StatusCode})", gvizUrl, TruncateResponse(content));
                }
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Ошибка загрузки листа «{SheetName}» через gviz. URL: {Url}, HTTP статус: {StatusCode}, Ответ: {ResponseBody}",
                    sheetName, gvizUrl, $"{(int)response.StatusCode} ({response.StatusCode})", TruncateResponse(errorBody));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Исключение при скачивании CSV для листа «{SheetName}» через gviz. URL: {Url}", sheetName, gvizUrl);
        }

        if (!string.IsNullOrWhiteSpace(gid))
        {
            var exportUrl = $"https://docs.google.com/spreadsheets/d/{_spreadsheetId}/export?format=csv&gid={gid}";
            try
            {
                _logger.LogInformation("Попытка резервной загрузки листа «{SheetName}» по gid={Gid}. URL: {Url}", sheetName, gid, exportUrl);
                var fallbackResponse = await _httpClient.GetAsync(exportUrl, cancellationToken);
                if (fallbackResponse.IsSuccessStatusCode)
                {
                    var fallbackContent = await fallbackResponse.Content.ReadAsStringAsync(cancellationToken);
                    if (IsValidCsvContent(fallbackContent))
                    {
                        if (!IsExpectedRatingSheet(sheetName) && (IsRatingSheetCsv(fallbackContent) || fallbackContent.Contains("ОБЩИЙ РЕЙТИНГ", StringComparison.OrdinalIgnoreCase)))
                        {
                            _logger.LogWarning("Запасной экспорт листа «{SheetName}» (gid={Gid}) вернул содержимое общего рейтинга. Считаем лист не найденным.", sheetName, gid);
                        }
                        else if (IsPlayersSheetName(sheetName) && !IsPlayersSheetCsv(fallbackContent))
                        {
                            _logger.LogWarning("Запасной экспорт листа «{SheetName}» (gid={Gid}) вернул другой лист (проверка заголовков не совпадает с листом игроков).", sheetName, gid);
                        }
                        else
                        {
                            _logger.LogInformation("Лист «{SheetName}» успешно загружен по запасному URL (gid={Gid}).", sheetName, gid);
                            return fallbackContent;
                        }
                    }
                    else
                    {
                        _logger.LogError(
                            "Запасной экспорт листа «{SheetName}» по gid={Gid} вернул HTTP {StatusCode}, но содержимое не является валидным CSV. URL: {Url}, Ответ: {ResponseBody}",
                            sheetName, gid, $"{(int)fallbackResponse.StatusCode} ({fallbackResponse.StatusCode})", exportUrl, TruncateResponse(fallbackContent));
                    }
                }
                else
                {
                    var fallbackErrorBody = await fallbackResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Ошибка скачивания CSV по запасному URL для листа «{SheetName}» (gid={Gid}). URL: {Url}, HTTP статус: {StatusCode}, Ответ: {ResponseBody}",
                        sheetName, gid, exportUrl, $"{(int)fallbackResponse.StatusCode} ({fallbackResponse.StatusCode})", TruncateResponse(fallbackErrorBody));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Исключение при скачивании CSV по запасному URL для листа «{SheetName}» (gid={Gid}). URL: {Url}", sheetName, gid, exportUrl);
            }
        }

        return null;
    }

    public bool IsExpectedRatingSheet(string? sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName)) return false;
        return IsRatingSheetName(sheetName) || 
               string.Equals(sheetName, _seasonSheetName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsPlayersSheetName(string? sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName)) return false;
        return string.Equals(sheetName, PlayersSheetName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sheetName, _playersSheetName, StringComparison.OrdinalIgnoreCase) ||
               sheetName.Contains("игрок", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRatingSheetName(string? sheetName)
    {
        if (string.IsNullOrWhiteSpace(sheetName)) return false;
        return string.Equals(sheetName, TotalRatingSheetName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sheetName, AutumnSeasonSheetName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sheetName, SeasonRatingSheetName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sheetName, LegacySeasonRatingSheetName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sheetName, DefaultSeasonName, StringComparison.OrdinalIgnoreCase) ||
               sheetName.Contains("рейтинг", StringComparison.OrdinalIgnoreCase) ||
               sheetName.Contains("сезон", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHtmlContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var trimmed = content.TrimStart().TrimStart('\uFEFF');
        return trimmed.StartsWith('<') ||
               trimmed.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("<body", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidCsvContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        if (IsHtmlContent(content)) return false;

        var trimmed = content.TrimStart().TrimStart('\uFEFF');
        if (trimmed.StartsWith("/*", StringComparison.Ordinal) ||
            trimmed.StartsWith('{') ||
            trimmed.Contains("google.visualization", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("\"status\":\"error\"", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("\"status\": \"error\"", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static string TruncateResponse(string? content, int maxLength = 2000)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        return content.Length <= maxLength ? content : content[..maxLength] + "... [truncated]";
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

    public record SheetPlayerCardInfo(
        string CardId,
        string FullName,
        string? Status,
        string? PhoneNumber = null
    );

    /// <summary>
    /// Извлекает и нормализует номер телефона до 10 цифр (например, из "Телефон: 89221648828" или "+7 904 847-31-61").
    /// </summary>
    public static string? ExtractAndNormalizePhoneNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var cleaned = raw.Replace('\u00A0', ' ').Trim();

        // 1. Поиск номера после явных телефонных маркеров ("тел", "телефон", "phone", "т.")
        var labeledMatch = Regex.Match(cleaned, @"(?:тел(?:ефон)?|phone|т\.?)\s*[:=\-]?\s*([+\d\s\-\(\)\.]{9,25})", RegexOptions.IgnoreCase);
        if (labeledMatch.Success)
        {
            var labeledDigits = new string(labeledMatch.Groups[1].Value.Where(char.IsDigit).ToArray());
            if (labeledDigits.Length == 11 && (labeledDigits[0] == '7' || labeledDigits[0] == '8'))
            {
                return labeledDigits[1..];
            }
            if (labeledDigits.Length == 10)
            {
                return labeledDigits;
            }
            if (labeledDigits.Length > 11 && (labeledDigits.StartsWith("7") || labeledDigits.StartsWith("8")))
            {
                return labeledDigits.Substring(1, 10);
            }
        }

        // 2. Точный поиск телефонного номера через Regex (на случай наличия дат или номеров карт в комментарии)
        var match = Regex.Match(cleaned, @"(?:\+?[78][\s\-\(\.]*)?(?:9\d{2}|[348]\d{2})[\s\-\)\.]*\d{3}[\s\-\.]*\d{2}[\s\-\.]*\d{2}");
        if (match.Success)
        {
            var matchDigits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (matchDigits.Length == 11 && (matchDigits[0] == '7' || matchDigits[0] == '8'))
            {
                return matchDigits[1..];
            }
            if (matchDigits.Length == 10)
            {
                return matchDigits;
            }
        }

        // 3. Общий случай: извлекаем все цифры из строки
        var digits = new string(cleaned.Where(char.IsDigit).ToArray());

        if (digits.Length == 11 && (digits.StartsWith("7") || digits.StartsWith("8")))
        {
            return digits[1..];
        }

        if (digits.Length == 10)
        {
            return digits;
        }

        return null;
    }

    /// <summary>
    /// Проверяет, является ли переданный CSV листом «Игроки» (по заголовкам колонок).
    /// </summary>
    public static bool IsPlayersSheetCsv(string? csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent)) return false;
        if (!IsValidCsvContent(csvContent)) return false;

        // Отклоняем, если контент содержит характерные заголовки таблицы рейтинга
        if (IsRatingSheetCsv(csvContent) ||
            csvContent.Contains("Сумма очков", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("ОБЩИЙ РЕЙТИНГ", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("Нокаутов", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("Среднее место", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Отклоняем, если контент содержит характерные заголовки таблицы регистраций турниров
        if (csvContent.Contains("ДАТА / ТУРНИР", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("РЕГИСТРАЦИИ", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("БАЙ-ИН", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("СТЕК", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rows = ParseCsv(csvContent);
        if (rows.Count == 0) return false;

        // Проверка заголовков в первых нескольких строках
        foreach (var row in rows.Take(5))
        {
            if (row.Count == 0) continue;

            var col0 = row[0].Replace('\u00A0', ' ').Trim();
            var col1 = row.Count > 1 ? row[1].Replace('\u00A0', ' ').Trim() : "";

            bool col0IsCardHeader = col0.Contains("id", StringComparison.OrdinalIgnoreCase) ||
                                    col0.Contains("номер", StringComparison.OrdinalIgnoreCase) ||
                                    col0.Contains("карт", StringComparison.OrdinalIgnoreCase) ||
                                    col0.Contains("№", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(col0, "игрок", StringComparison.OrdinalIgnoreCase);

            bool col1IsNameHeader = string.Equals(col1, "фио", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(col1, "имя", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(col1, "игрок", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(col1, "фио игрока", StringComparison.OrdinalIgnoreCase) ||
                                    col1.Contains("фио", StringComparison.OrdinalIgnoreCase) ||
                                    col1.Contains("имя", StringComparison.OrdinalIgnoreCase);

            if (col0IsCardHeader && (col1IsNameHeader || row.Count == 1))
            {
                return true;
            }

            bool hasId = row.Any(c => c.Contains("id", StringComparison.OrdinalIgnoreCase) || c.Contains("карт", StringComparison.OrdinalIgnoreCase));
            bool hasStatusOrPhone = row.Any(c => c.Contains("статус", StringComparison.OrdinalIgnoreCase) || c.Contains("телефон", StringComparison.OrdinalIgnoreCase));
            if (hasId && hasStatusOrPhone)
            {
                return true;
            }
        }

        // Если заголовков нет, но первая строка явно похожа на валидную карту клуба
        var firstRow = rows[0];
        if (firstRow.Count >= 1)
        {
            var firstVal = firstRow[0].Replace('\u00A0', ' ').Trim().TrimStart('№', '#').Trim();
            if (firstVal.Length > 0 && firstVal.Length <= 10 && firstVal.All(char.IsLetterOrDigit) &&
                !firstVal.Contains('.') && !firstVal.Contains('/') && !firstVal.Contains(':'))
            {
                return true;
            }
        }

        return false;
    }

    public static List<SheetPlayerCardInfo> ParsePlayersSheet(string csvContent)
    {
        var result = new List<SheetPlayerCardInfo>();
        if (string.IsNullOrWhiteSpace(csvContent)) return result;

        if (!IsPlayersSheetCsv(csvContent))
        {
            return result;
        }

        var parsedRows = ParseCsv(csvContent);
        foreach (var row in parsedRows)
        {
            if (row.Count < 1) continue;

            var col0 = row[0].Replace('\u00A0', ' ').Trim().TrimStart('№', '#').Trim();
            var col1 = row.Count > 1 ? row[1].Replace('\u00A0', ' ').Trim() : "";
            var col2 = row.Count > 2 ? row[2].Replace('\u00A0', ' ').Trim() : null;
            var col3 = row.Count > 3 ? row[3].Replace('\u00A0', ' ').Trim() : null;
            var col4 = row.Count > 4 ? row[4].Replace('\u00A0', ' ').Trim() : null;
            var col5 = row.Count > 5 ? row[5].Replace('\u00A0', ' ').Trim() : null;

            // Пропускаем строку заголовка
            if (col0.Contains("id", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("номер", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("карт", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("№", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "игрок", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "фио", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "имя", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "игрок", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "фио игрока", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(col0)) continue;

            // Игнорируем невалидные плейсхолдеры для карты
            if (string.Equals(col0, "-", StringComparison.Ordinal) ||
                string.Equals(col0, "нет", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "б/н", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "none", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "null", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "0", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsCardStatusValid(col4))
            {
                continue;
            }

            // Извлечение телефона:
            // 1) В первую очередь из колонки [5] Комментарий (структура по фото заказчика)
            var phone = ExtractAndNormalizePhoneNumber(col5);

            // 2) Если в [5] телефона нет, проверяем колонку [3] ("Телефон" в 6-колоночной структуре по фото)
            if (string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(col3))
            {
                phone = ExtractAndNormalizePhoneNumber(col3);
            }

            // 3) Если в [3] телефона нет, проверяем колонку [2] (в 5-колоночной структуре "Телефон" был в [2])
            if (string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(col2))
            {
                phone = ExtractAndNormalizePhoneNumber(col2);
            }

            result.Add(new SheetPlayerCardInfo(col0, col1, col4, phone));
        }

        return result;
    }

    public static HashSet<string> ParseBlockedCardIds(string csvContent)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csvContent)) return result;
        if (!IsPlayersSheetCsv(csvContent)) return result;

        var parsedRows = ParseCsv(csvContent);
        foreach (var row in parsedRows)
        {
            if (row.Count < 1) continue;

            var col0 = row[0].Replace('\u00A0', ' ').Trim().TrimStart('№', '#').Trim();
            var col1 = row.Count > 1 ? row[1].Replace('\u00A0', ' ').Trim() : "";
            var col4 = row.Count > 4 ? row[4].Replace('\u00A0', ' ').Trim() : null;

            // Пропускаем строку заголовка
            if (col0.Contains("id", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("номер", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("карт", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("№", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "игрок", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "фио", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "имя", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "игрок", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col1, "фио игрока", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(col0)) continue;

            if (string.Equals(col0, "-", StringComparison.Ordinal) ||
                string.Equals(col0, "нет", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "б/н", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "none", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "null", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(col0, "0", StringComparison.Ordinal))
            {
                continue;
            }

            if (!IsCardStatusValid(col4))
            {
                result.Add(col0);
            }
        }

        return result;
    }

    public static bool IsCardStatusValid(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        var s = status.Trim().ToLowerInvariant();
        if (s.Contains("блок") || s.Contains("заблокир") || s.Contains("бан") ||
            s.Contains("утер") || s.Contains("аннул") || s.Contains("неактив") || s.Contains("не актив") ||
            s.Contains("черн") || s.Contains("чс") || s.Contains("архив") ||
            s.Contains("приостанов") || s.Contains("стоп"))
        {
            return false;
        }

        return true;
    }

    public Task<GoogleSheetsSyncResult> SyncFromCsvAsync(
        string? seasonRatingCsvContent,
        string? totalRatingCsvContent,
        string? registrationsCsvContent,
        CancellationToken cancellationToken = default)
    {
        return SyncFromCsvAsync(seasonRatingCsvContent, totalRatingCsvContent, registrationsCsvContent, null, cancellationToken);
    }

    public async Task<GoogleSheetsSyncResult> SyncFromCsvAsync(
        string? seasonRatingCsvContent,
        string? totalRatingCsvContent,
        string? registrationsCsvContent,
        string? playersCsvContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(seasonRatingCsvContent) && 
            string.IsNullOrWhiteSpace(totalRatingCsvContent) &&
            string.IsNullOrWhiteSpace(playersCsvContent))
        {
            return new GoogleSheetsSyncResult(false, 0, 0, 0, "CSV контент пуст.");
        }

        if (!string.IsNullOrWhiteSpace(seasonRatingCsvContent))
        {
            var parsedSeason = ExtractSeasonName(seasonRatingCsvContent);
            if (!string.IsNullOrWhiteSpace(parsedSeason))
            {
                CurrentSeasonName = parsedSeason;
            }
        }

        var hasSeasonSheet = !string.IsNullOrWhiteSpace(seasonRatingCsvContent);
        var seasonRows = ParseRatingSheet(seasonRatingCsvContent ?? "");
        var totalRows = ParseRatingSheet(totalRatingCsvContent ?? "");
        var playersRows = ParsePlayersSheet(playersCsvContent ?? "");
        var blockedCardIds = ParseBlockedCardIds(playersCsvContent ?? "");

        if (seasonRows.Count == 0 && totalRows.Count == 0 && playersRows.Count == 0 && blockedCardIds.Count == 0)
        {
            return new GoogleSheetsSyncResult(false, 0, 0, 0, "Не удалось распознать строки в CSV.");
        }

        var regMap = ParseRegistrationsMapping(registrationsCsvContent ?? "");
        var existingUsers = await _context.Users.ToListAsync(cancellationToken);

        var processedUsers = new HashSet<User>();
        var updatedUsers = new HashSet<User>();
        var createdUsers = new HashSet<User>();

        // Очистка ранее поврежденных данных из-за бага смещения колонок:
        // Если у пользователя телефон состоит менее чем из 5 цифр (например, "3" - количество побед), очищаем его.
        // Если номер карты равен числу сыгранных турниров и не подтвержден в листе регистраций, очищаем его.
        foreach (var u in existingUsers)
        {
            if (u.PhoneNumber != null && u.PhoneNumber.Count(char.IsDigit) < 5)
            {
                u.PhoneNumber = null;
                processedUsers.Add(u);
                updatedUsers.Add(u);
            }

            if (!string.IsNullOrWhiteSpace(u.ClubCardId) && 
                u.TournamentsPlayed > 0 && 
                string.Equals(u.ClubCardId.Trim(), u.TournamentsPlayed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var norm = NormalizeNameKey($"{u.FirstName} {u.LastName}");
                bool confirmedInReg = regMap.TryGetValue(norm, out var reg) && 
                                     !string.IsNullOrWhiteSpace(reg.ClubCardId) &&
                                     string.Equals(reg.ClubCardId.Trim(), u.ClubCardId.Trim(), StringComparison.OrdinalIgnoreCase);
                bool confirmedInPlayers = playersRows.Any(p => 
                    string.Equals(p.CardId.TrimStart('№', '#').Trim(), u.ClubCardId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!confirmedInReg && !confirmedInPlayers)
                {
                    u.ClubCardId = null;
                    processedUsers.Add(u);
                    updatedUsers.Add(u);
                }
            }

            // Очистка ошибочных ClubCardId, равных SheetRank (месту в рейтинге)
            if (u.SheetRank.HasValue && !string.IsNullOrWhiteSpace(u.ClubCardId) &&
                string.Equals(u.ClubCardId.Trim(), u.SheetRank.Value.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                var norm = NormalizeNameKey($"{u.FirstName} {u.LastName}");
                bool confirmedInReg = regMap.TryGetValue(norm, out var reg) && 
                                     !string.IsNullOrWhiteSpace(reg.ClubCardId) &&
                                     string.Equals(reg.ClubCardId.Trim(), u.ClubCardId.Trim(), StringComparison.OrdinalIgnoreCase);
                bool confirmedInPlayers = playersRows.Any(p => 
                    string.Equals(p.CardId.TrimStart('№', '#').Trim(), u.ClubCardId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!confirmedInReg && !confirmedInPlayers)
                {
                    u.ClubCardId = null;
                    processedUsers.Add(u);
                    updatedUsers.Add(u);
                }
            }
        }

        // 1. Обработка Общего рейтинга (пишет в TotalRating)
        foreach (var row in totalRows)
        {
            var normKey = NormalizeNameKey(row.PlayerName);
            string? knownCardId = null;

            if (regMap.TryGetValue(normKey, out var regInfo))
            {
                knownCardId = regInfo.ClubCardId;
            }

            var matchedUser = FindMatchingUser(existingUsers, row.PlayerName, knownCardId);
            if (matchedUser != null)
            {
                matchedUser.TotalRating = row.Points;
                matchedUser.SheetRank = row.Place;
                matchedUser.TournamentsPlayed = row.TournamentsPlayed;
                matchedUser.WinsCount = row.WinsCount;
                matchedUser.Top3Count = row.Top3Count;
                matchedUser.Top10Count = row.Top10Count;
                matchedUser.KnockoutsCount = row.KnockoutsCount;
                matchedUser.AvgPlace = row.AvgPlace;

                // Если предоставлен лист сезона, сбрасываем SeasonRating в 0 (будет установлен в row.Points ниже, если игрок есть в seasonRows)
                if (hasSeasonSheet)
                {
                    matchedUser.SeasonRating = 0;
                }

                // Очистка ошибочных данных из старого смещения колонок:
                if (matchedUser.PhoneNumber != null && matchedUser.PhoneNumber.Count(char.IsDigit) < 5)
                {
                    matchedUser.PhoneNumber = null;
                }

                if (matchedUser.ClubCardId != null && 
                    ((matchedUser.ClubCardId == row.TournamentsPlayed.ToString()) || (matchedUser.ClubCardId == row.Place.ToString())) && 
                    !regMap.ContainsKey(NormalizeNameKey(row.PlayerName)))
                {
                    matchedUser.ClubCardId = null;
                }

                // ВНИМАНИЕ: НИ В КОЕМ СЛУЧАЕ НЕ ПРИСВАИВАТЬ ClubCardId И PhoneNumber ИЗ ЭТОГО ЛИСТА!

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
                    SheetRank = row.Place,
                    SeasonRating = 0,
                    TournamentsPlayed = row.TournamentsPlayed,
                    WinsCount = row.WinsCount,
                    Top3Count = row.Top3Count,
                    Top10Count = row.Top10Count,
                    KnockoutsCount = row.KnockoutsCount,
                    AvgPlace = row.AvgPlace,
                    ClubCardId = null, // НИ В КОЕМ СЛУЧАЕ НЕ ПРИСВАИВАТЬ ИЗ ЭТОГО ЛИСТА!
                    PhoneNumber = null, // НИ В КОЕМ СЛУЧАЕ НЕ ПРИСВАИВАТЬ ИЗ ЭТОГО ЛИСТА!
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                existingUsers.Add(newUser);
                processedUsers.Add(newUser);
                createdUsers.Add(newUser);
            }
        }

        // Если лист сезона загружен и на нем 0 очков или нет игр (seasonRows пуст или у всех 0 очков),
        // гарантируем, что для ВСЕХ пользователей SeasonRating = 0, сохраняя несгораемый TotalRating
        bool isSeasonEmptyOrZero = hasSeasonSheet && (seasonRows.Count == 0 || seasonRows.All(r => r.Points == 0));
        if (isSeasonEmptyOrZero)
        {
            foreach (var u in existingUsers)
            {
                if (u.SeasonRating != 0)
                {
                    u.SeasonRating = 0;
                    processedUsers.Add(u);
                    if (!createdUsers.Contains(u))
                    {
                        updatedUsers.Add(u);
                    }
                }
            }
        }

        // 2. Обработка Рейтинга сезона (пишет в SeasonRating)
        var seasonMatchedUsers = new HashSet<User>();
        foreach (var row in seasonRows)
        {
            var normKey = NormalizeNameKey(row.PlayerName);
            string? knownCardId = null;

            if (regMap.TryGetValue(normKey, out var regInfo))
            {
                knownCardId = regInfo.ClubCardId;
            }

            var matchedUser = FindMatchingUser(existingUsers, row.PlayerName, knownCardId);
            if (matchedUser != null)
            {
                matchedUser.SeasonRating = row.Points;
                if (matchedUser.SheetRank == null || totalRows.Count == 0)
                {
                    matchedUser.SheetRank = row.Place;
                }
                seasonMatchedUsers.Add(matchedUser);

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

                // Очистка ошибочных данных из старого смещения колонок:
                if (matchedUser.PhoneNumber != null && matchedUser.PhoneNumber.Count(char.IsDigit) < 5)
                {
                    matchedUser.PhoneNumber = null;
                }

                if (matchedUser.ClubCardId != null && 
                    ((matchedUser.ClubCardId == row.TournamentsPlayed.ToString()) || (matchedUser.ClubCardId == row.Place.ToString())) && 
                    !regMap.ContainsKey(NormalizeNameKey(row.PlayerName)))
                {
                    matchedUser.ClubCardId = null;
                }

                // ВНИМАНИЕ: НИ В КОЕМ СЛУЧАЕ НЕ ПРИСВАИВАТЬ ClubCardId И PhoneNumber ИЗ ЭТОГО ЛИСТА!

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
                    SheetRank = row.Place,
                    TotalRating = 0,
                    TournamentsPlayed = row.TournamentsPlayed,
                    WinsCount = row.WinsCount,
                    Top3Count = row.Top3Count,
                    Top10Count = row.Top10Count,
                    KnockoutsCount = row.KnockoutsCount,
                    AvgPlace = row.AvgPlace,
                    ClubCardId = null, // НИ В КОЕМ СЛУЧАЕ НЕ ПРИСВАИВАТЬ ИЗ ЭТОГО ЛИСТА!
                    PhoneNumber = null, // НИ В КОЕМ СЛУЧАЕ НЕ ПРИСВАИВАТЬ ИЗ ЭТОГО ЛИСТА!
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                existingUsers.Add(newUser);
                processedUsers.Add(newUser);
                createdUsers.Add(newUser);
                seasonMatchedUsers.Add(newUser);
            }
        }

        // Если лист сезона загружен и содержит ненулевые очки,
        // все пользователи, не вошедшие в seasonRows, должны иметь SeasonRating = 0 в текущем сезоне
        if (hasSeasonSheet && !isSeasonEmptyOrZero)
        {
            foreach (var u in existingUsers)
            {
                if (!seasonMatchedUsers.Contains(u) && u.SeasonRating != 0)
                {
                    u.SeasonRating = 0;
                    processedUsers.Add(u);
                    if (!createdUsers.Contains(u))
                    {
                        updatedUsers.Add(u);
                    }
                }
            }
        }

        // 3. Аккуратно подтягиваем ClubCardId и PhoneNumber из листа «РЕГИСТРАЦИИ» строго по совпадению ФИО
        if (regMap.Count > 0)
        {
            foreach (var (normKey, regInfo) in regMap)
            {
                // Ищем игрока по нормализованному ФИО (без сопоставления по фейковым ID)
                var matchedUser = FindMatchingUserByName(existingUsers, regInfo.FullName);

                // Если не найден по ФИО, проверяем резервный поиск по реальному ClubCardId
                if (matchedUser == null && !string.IsNullOrWhiteSpace(regInfo.ClubCardId))
                {
                    var trimmedCard = regInfo.ClubCardId.Trim();
                    matchedUser = existingUsers.FirstOrDefault(u => 
                        !string.IsNullOrWhiteSpace(u.ClubCardId) && 
                        string.Equals(u.ClubCardId.Trim(), trimmedCard, StringComparison.OrdinalIgnoreCase));
                }

                if (matchedUser != null)
                {
                    bool modified = false;

                    if (!MasterClubCardConstants.IsMasterAdminCard(matchedUser.ClubCardId) &&
                        !string.IsNullOrWhiteSpace(regInfo.ClubCardId) && 
                        !string.Equals(matchedUser.ClubCardId, regInfo.ClubCardId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedUser.ClubCardId = regInfo.ClubCardId;
                        modified = true;
                    }

                    if (!string.IsNullOrWhiteSpace(regInfo.PhoneNumber) && 
                        (string.IsNullOrWhiteSpace(matchedUser.PhoneNumber) || 
                         matchedUser.PhoneNumber.Count(char.IsDigit) < 5 ||
                         !string.Equals(matchedUser.PhoneNumber, regInfo.PhoneNumber, StringComparison.OrdinalIgnoreCase)))
                    {
                        matchedUser.PhoneNumber = regInfo.PhoneNumber;
                        modified = true;
                    }

                    if (modified)
                    {
                        processedUsers.Add(matchedUser);
                        if (!createdUsers.Contains(matchedUser))
                        {
                            updatedUsers.Add(matchedUser);
                        }
                    }
                }
            }
        }

        // 4. Загрузка и привязка клубных карт из листа «Игроки» (белый список карт клуба)
        if (blockedCardIds.Count > 0)
        {
            foreach (var blockedCard in blockedCardIds)
            {
                var stubsToRemove = existingUsers
                    .Where(u => (u.VkId.StartsWith("sheet_card_") || u.VkId.StartsWith("card_")) &&
                                u.ClubCardId != null &&
                                string.Equals(u.ClubCardId.Trim(), blockedCard, StringComparison.OrdinalIgnoreCase) &&
                                u.TotalRating == 0 && u.TournamentsPlayed == 0)
                    .ToList();

                foreach (var stub in stubsToRemove)
                {
                    _context.Users.Remove(stub);
                    existingUsers.Remove(stub);
                    processedUsers.Remove(stub);
                    createdUsers.Remove(stub);
                    updatedUsers.Remove(stub);
                }
            }
        }

        if (playersRows.Count > 0)
        {
            foreach (var playerCard in playersRows)
            {
                if (string.IsNullOrWhiteSpace(playerCard.CardId)) continue;
                var trimmedCard = playerCard.CardId.Trim().TrimStart('№', '#').Trim();
                if (string.IsNullOrWhiteSpace(trimmedCard)) continue;

                // 1. Ищем существующего пользователя по ФИО (например, уже создан из листа рейтинга)
                User? matchedByName = !string.IsNullOrWhiteSpace(playerCard.FullName)
                    ? FindMatchingUserByName(existingUsers, playerCard.FullName)
                    : null;

                // 2. Ищем существующего пользователя по ClubCardId
                var matchedByCard = existingUsers.FirstOrDefault(u => 
                    !string.IsNullOrWhiteSpace(u.ClubCardId) && 
                    string.Equals(u.ClubCardId.Trim(), trimmedCard, StringComparison.OrdinalIgnoreCase));

                // Если игрок найден по ФИО (из рейтинга или ранее созданный):
                if (matchedByName != null)
                {
                    bool modified = false;

                    // Привязываем ClubCardId (не перезаписывая мастер-карту админа)
                    if (!MasterClubCardConstants.IsMasterAdminCard(matchedByName.ClubCardId))
                    {
                        if (string.IsNullOrWhiteSpace(matchedByName.ClubCardId) ||
                            ((matchedByName.VkId.StartsWith("sheet_") || matchedByName.VkId.StartsWith("card_")) && 
                             !string.Equals(matchedByName.ClubCardId.Trim(), trimmedCard, StringComparison.OrdinalIgnoreCase)))
                        {
                            matchedByName.ClubCardId = trimmedCard;
                            modified = true;
                        }
                    }

                    // Привязываем нормализованный телефон
                    if (!string.IsNullOrWhiteSpace(playerCard.PhoneNumber))
                    {
                        if (string.IsNullOrWhiteSpace(matchedByName.PhoneNumber) ||
                            matchedByName.PhoneNumber.Count(char.IsDigit) < 5 ||
                            ((matchedByName.VkId.StartsWith("sheet_") || matchedByName.VkId.StartsWith("card_")) && 
                             !string.Equals(matchedByName.PhoneNumber, playerCard.PhoneNumber, StringComparison.OrdinalIgnoreCase)))
                        {
                            matchedByName.PhoneNumber = playerCard.PhoneNumber;
                            modified = true;
                        }
                    }

                    // Если у пользователя заглушечные имена "Игрок", обновляем из листа «Игроки»
                    if (!string.IsNullOrWhiteSpace(playerCard.FullName) && 
                        (matchedByName.VkId.StartsWith("sheet_") || matchedByName.VkId.StartsWith("card_")) &&
                        (matchedByName.FirstName == "Игрок" || string.IsNullOrWhiteSpace(matchedByName.LastName)))
                    {
                        var parts = playerCard.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        string lName = parts.Length > 0 ? parts[0] : playerCard.FullName;
                        string fName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "Игрок";
                        matchedByName.LastName = lName;
                        matchedByName.FirstName = fName;
                        modified = true;
                    }

                    // Если был отдельный stub-пользователь с этой же картой, удаляем его во избежание дубликатов
                    if (matchedByCard != null && matchedByCard.Id != matchedByName.Id &&
                        (matchedByCard.VkId.StartsWith("sheet_card_") || matchedByCard.VkId.StartsWith("card_")) &&
                        matchedByCard.TotalRating == 0 && matchedByCard.TournamentsPlayed == 0)
                    {
                        var dupRegs = await _context.Registrations.Where(r => r.UserId == matchedByCard.Id).ToListAsync(cancellationToken);
                        foreach (var r in dupRegs)
                        {
                            r.UserId = matchedByName.Id;
                        }

                        _context.Users.Remove(matchedByCard);
                        existingUsers.Remove(matchedByCard);
                        processedUsers.Remove(matchedByCard);
                        createdUsers.Remove(matchedByCard);
                        updatedUsers.Remove(matchedByCard);
                    }

                    if (modified)
                    {
                        processedUsers.Add(matchedByName);
                        if (!createdUsers.Contains(matchedByName)) updatedUsers.Add(matchedByName);
                    }

                    // Если пользователь по имени найден, но у него уже другая карта (и мы ее не меняем),
                    // а самой карты trimmedCard еще нет в базе — создаем для нее stub-пользователя в белом списке
                    if (!string.Equals(matchedByName.ClubCardId?.Trim(), trimmedCard, StringComparison.OrdinalIgnoreCase) &&
                        matchedByCard == null &&
                        !existingUsers.Any(u => string.Equals(u.ClubCardId?.Trim(), trimmedCard, StringComparison.OrdinalIgnoreCase)))
                    {
                        var nameParts = playerCard.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        string lastName = nameParts.Length > 0 ? nameParts[0] : playerCard.FullName;
                        string firstName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "Игрок";

                        var uid = Guid.NewGuid().ToString("N")[..8];
                        var newVkId = $"sheet_card_{trimmedCard}_{uid}";
                        if (newVkId.Length > 50) newVkId = newVkId[..50];

                        var stubForCard = new User
                        {
                            VkId = newVkId,
                            FirstName = firstName,
                            LastName = lastName,
                            ClubCardId = trimmedCard,
                            PhoneNumber = playerCard.PhoneNumber,
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

                        _context.Users.Add(stubForCard);
                        existingUsers.Add(stubForCard);
                        processedUsers.Add(stubForCard);
                        createdUsers.Add(stubForCard);
                    }
                }
                // 3. Если по имени не найден, но найден по ClubCardId
                else if (matchedByCard != null)
                {
                    bool modified = false;

                    // Если это stub-пользователь из листа карт, обновляем ФИО
                    if ((matchedByCard.VkId.StartsWith("sheet_card_") || matchedByCard.VkId.StartsWith("card_")) && 
                        !string.IsNullOrWhiteSpace(playerCard.FullName))
                    {
                        var parts = playerCard.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        string lName = parts.Length > 0 ? parts[0] : playerCard.FullName;
                        string fName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "Игрок";
                        if (matchedByCard.FirstName != fName || matchedByCard.LastName != lName)
                        {
                            matchedByCard.FirstName = fName;
                            matchedByCard.LastName = lName;
                            modified = true;
                        }
                    }

                    // Обновляем телефон
                    if (!string.IsNullOrWhiteSpace(playerCard.PhoneNumber) &&
                        (string.IsNullOrWhiteSpace(matchedByCard.PhoneNumber) ||
                         ((matchedByCard.VkId.StartsWith("sheet_") || matchedByCard.VkId.StartsWith("card_")) && 
                          !string.Equals(matchedByCard.PhoneNumber, playerCard.PhoneNumber, StringComparison.OrdinalIgnoreCase))))
                    {
                        matchedByCard.PhoneNumber = playerCard.PhoneNumber;
                        modified = true;
                    }

                    if (modified)
                    {
                        processedUsers.Add(matchedByCard);
                        if (!createdUsers.Contains(matchedByCard)) updatedUsers.Add(matchedByCard);
                    }
                }
                // 4. Если такого игрока/карты нет в базе, создаем нового пользователя в белом списке
                else
                {
                    var nameParts = playerCard.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string lastName = nameParts.Length > 0 ? nameParts[0] : playerCard.FullName;
                    string firstName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "Игрок";

                    var uid = Guid.NewGuid().ToString("N")[..8];
                    var newVkId = $"sheet_card_{trimmedCard}_{uid}";
                    if (newVkId.Length > 50) newVkId = newVkId[..50];

                    var newUser = new User
                    {
                        VkId = newVkId,
                        FirstName = firstName,
                        LastName = lastName,
                        ClubCardId = trimmedCard,
                        PhoneNumber = playerCard.PhoneNumber,
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

                    _context.Users.Add(newUser);
                    existingUsers.Add(newUser);
                    processedUsers.Add(newUser);
                    createdUsers.Add(newUser);
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            _cacheResetToken?.Reset();
        }
        catch
        {
            // Игнорируем ошибки при сбросе кэша
        }

        int totalProcessed = processedUsers.Count;
        int createdCount = createdUsers.Count;
        int updatedCount = updatedUsers.Count;

        var message = $"Синхронизация Google Sheets завершена: обработано {totalProcessed}, обновлено {updatedCount}, создано {createdCount}.";
        _logger.LogInformation("{SyncMessage}", message);

        return new GoogleSheetsSyncResult(true, totalProcessed, updatedCount, createdCount, message);
    }

    public static User? FindMatchingUserByName(
        IEnumerable<User> users, 
        string playerName)
    {
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

            // Сопоставление по Никнейму
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

    public static User? FindMatchingUser(
        IEnumerable<User> users, 
        string playerName, 
        string? clubCardId = null)
    {
        // 1. Приоритетное сопоставление по токенам ФИО
        var byName = FindMatchingUserByName(users, playerName);
        if (byName != null) return byName;

        // 2. Резервное сопоставление по реальному ClubCardId (только если передан и по ФИО пользователь не найден)
        if (!string.IsNullOrWhiteSpace(clubCardId))
        {
            var trimmedCard = clubCardId.Trim();
            var byCard = users.FirstOrDefault(u => 
                !string.IsNullOrWhiteSpace(u.ClubCardId) && 
                string.Equals(u.ClubCardId.Trim(), trimmedCard, StringComparison.OrdinalIgnoreCase));
            if (byCard != null) return byCard;
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

    public record PlayerCardInfo(string? ClubCardId, string? PhoneNumber, string FullName);

    public static bool IsRatingSheetCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return false;

        bool hasRatingKeywords = 
            csv.Contains("Сумма очков", StringComparison.OrdinalIgnoreCase) ||
            csv.Contains("Нокаутов", StringComparison.OrdinalIgnoreCase) ||
            csv.Contains("Среднее место", StringComparison.OrdinalIgnoreCase) ||
            csv.Contains("ОБЩИЙ РЕЙТИНГ", StringComparison.OrdinalIgnoreCase);

        bool hasRegistrationKeywords =
            csv.Contains("РЕГИСТРАЦИИ", StringComparison.OrdinalIgnoreCase) ||
            csv.Contains("ДАТА / ТУРНИР", StringComparison.OrdinalIgnoreCase) ||
            csv.Contains("ТЕЛЕФОН", StringComparison.OrdinalIgnoreCase);

        return hasRatingKeywords && !hasRegistrationKeywords;
    }

    public static Dictionary<string, PlayerCardInfo> ParseRegistrationsMapping(string registrationsCsv)
    {
        var mapping = new Dictionary<string, PlayerCardInfo>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(registrationsCsv)) return mapping;

        // Если контент явно является рейтинговой таблицей (содержит колонки рейтинга), не парсим как регистрации!
        if (IsRatingSheetCsv(registrationsCsv))
        {
            return mapping;
        }

        var rows = ParseCsv(registrationsCsv);
        foreach (var row in rows)
        {
            if (row.Count < 2) continue;

            var col0 = row[0].Trim();
            var col1 = row[1].Trim(); // ИМЯ
            var col2 = row.Count > 2 ? row[2].Trim() : null; // ID (ClubCardId)
            var col3 = row.Count > 3 ? row[3].Trim() : null; // ТЕЛЕФОН

            // Пропускаем заголовки
            if ((col0.Contains("дата", StringComparison.OrdinalIgnoreCase) && col0.Contains("турнир", StringComparison.OrdinalIgnoreCase)) ||
                col0.Equals("турнир", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("место", StringComparison.OrdinalIgnoreCase) ||
                col0.Contains("рейтинг", StringComparison.OrdinalIgnoreCase) ||
                col1.Contains("имя", StringComparison.OrdinalIgnoreCase) ||
                col1.Contains("игрок", StringComparison.OrdinalIgnoreCase) ||
                (col2 != null && (col2.Equals("id", StringComparison.OrdinalIgnoreCase) || col2.Contains("турниров", StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            // Если строка похожа на строку рейтинга (col0 - число места и >= 6 колонок), пропускаем
            if (int.TryParse(col0, out _) && row.Count >= 6)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(col1))
                continue;

            var cardId = !string.IsNullOrWhiteSpace(col2) ? col2.Trim() : null;
            var playerName = col1.Trim();
            var phone = !string.IsNullOrWhiteSpace(col3) ? col3.Trim() : null;

            // Игнорируем невалидные плейсхолдеры для карты
            if (cardId != null && (
                string.Equals(cardId, "-", StringComparison.Ordinal) ||
                string.Equals(cardId, "нет", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cardId, "б/н", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cardId, "none", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cardId, "null", StringComparison.OrdinalIgnoreCase)))
            {
                cardId = null;
            }

            // Валидация телефона: номер телефона не может быть 1-2 цифрами (как количество побед "3")
            if (phone != null && phone.Count(char.IsDigit) < 5)
            {
                phone = null;
            }

            if (string.IsNullOrWhiteSpace(cardId) && string.IsNullOrWhiteSpace(phone))
            {
                continue;
            }

            var info = new PlayerCardInfo(cardId, phone, playerName);

            var normKey = NormalizeNameKey(playerName);
            if (!string.IsNullOrEmpty(normKey))
            {
                if (mapping.TryGetValue(normKey, out var existingInfo))
                {
                    var bestCard = !string.IsNullOrWhiteSpace(existingInfo.ClubCardId) ? existingInfo.ClubCardId : cardId;
                    var bestPhone = !string.IsNullOrWhiteSpace(existingInfo.PhoneNumber) ? existingInfo.PhoneNumber : phone;
                    mapping[normKey] = new PlayerCardInfo(bestCard, bestPhone, playerName);
                }
                else
                {
                    mapping[normKey] = info;
                }
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

    public static string ExtractSeasonName(string? csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            return DefaultSeasonName;

        if (csvContent.Contains("ОСЕННЕГО СЕЗОНА 2026", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("Осенний сезон 2026", StringComparison.OrdinalIgnoreCase) ||
            csvContent.Contains("AUTUMN SEASON 2026", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultSeasonName;
        }

        var rows = ParseCsv(csvContent);
        if (rows.Count == 0)
            return DefaultSeasonName;

        // 1. Ищем строку, содержащую ключевое слово "сезон" (в первых 10 строках, до строк игроков)
        for (int i = 0; i < Math.Min(10, rows.Count); i++)
        {
            var row = rows[i];
            if (row.Count == 0) continue;

            // Если это строка игрока (начинается с числа места, например "1"), прекращаем поиск до таблицы
            if (int.TryParse(row[0].Trim(), out _))
                break;

            // Вариант 1a: Ячейка 0 содержит "сезон" (например, "Выбранный сезон:"), а значение в ячейке 1 (B2 или B3)
            if (row[0].Contains("сезон", StringComparison.OrdinalIgnoreCase))
            {
                if (row.Count > 1 && !string.IsNullOrWhiteSpace(row[1]))
                {
                    var val = CleanSeasonName(row[1]);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }

                var colonIdx = row[0].IndexOf(':');
                if (colonIdx >= 0 && colonIdx + 1 < row[0].Length)
                {
                    var val = CleanSeasonName(row[0][(colonIdx + 1)..]);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }

                if (row[0].Trim().StartsWith("сезон", StringComparison.OrdinalIgnoreCase) && row[0].Length > 5)
                {
                    var val = CleanSeasonName(row[0][5..]);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
            }

            // Вариант 1b: Ячейка 1 содержит "сезон"
            if (row.Count > 1 && row[1].Contains("сезон", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = row[1].IndexOf(':');
                if (colonIdx >= 0 && colonIdx + 1 < row[1].Length)
                {
                    var val = CleanSeasonName(row[1][(colonIdx + 1)..]);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
                if (row[1].Trim().StartsWith("сезон", StringComparison.OrdinalIgnoreCase) && row[1].Length > 5)
                {
                    var val = CleanSeasonName(row[1][5..]);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
                if (row.Count > 2 && !string.IsNullOrWhiteSpace(row[2]))
                {
                    var val = CleanSeasonName(row[2]);
                    if (!string.IsNullOrEmpty(val))
                        return val;
                }
            }
        }

        // 2. Проверяем формат gviz (где заголовки объединены в первой строке: "ТЕХНИЧЕСКОЕ ОТКРЫТИЕ Игрок")
        var headerRow = rows[0];
        if (headerRow.Count > 1 && !int.TryParse(headerRow[0].Trim(), out _))
        {
            var col1 = headerRow[1];
            if (col1.EndsWith("Игрок", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = col1[..col1.LastIndexOf("Игрок", StringComparison.OrdinalIgnoreCase)].Trim();
                var val = CleanSeasonName(candidate);
                if (!string.IsNullOrEmpty(val))
                    return val;
            }
        }

        // 3. Проверяем ячейку B3 (строка индекс 2, колонка индекс 1)
        // ВНИМАНИЕ: B3 имеет приоритет согласно ТЗ ("ячейка B3 листа «Рейтинг сезона»")
        if (rows.Count > 2 && rows[2].Count > 1 && !int.TryParse(rows[2][0].Trim(), out _))
        {
            var candidateB3 = CleanSeasonName(rows[2][1]);
            if (!string.IsNullOrEmpty(candidateB3))
                return candidateB3;
        }

        // 4. Проверяем ячейку B2 (строка индекс 1, колонка индекс 1)
        if (rows.Count > 1 && rows[1].Count > 1 && !int.TryParse(rows[1][0].Trim(), out _))
        {
            var candidateB2 = CleanSeasonName(rows[1][1]);
            if (!string.IsNullOrEmpty(candidateB2))
                return candidateB2;
        }

        return DefaultSeasonName;
    }

    public static string? CleanSeasonName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var trimmed = raw.Replace('\u00A0', ' ').Trim().Trim('"', '\'', ':', ' ', '\t');
        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        if (trimmed.StartsWith("Сезон:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[6..].Trim().Trim('"', '\'', ':', ' ', '\t');
        }
        else if (trimmed.StartsWith("Сезон", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[5..].Trim().Trim('"', '\'', ':', ' ', '\t');
        }

        if (trimmed.EndsWith("Игрок", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^5].Trim().Trim('"', '\'', ':', ' ', '\t');
        }

        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        if (trimmed.Equals("Игрок", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Место", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Турниров", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Побед", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ТОП-3", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ТОП 3", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ТОП-10", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ТОП 10", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Нокаутов", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Сумма очков", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Среднее место", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Выбранный сезон", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Сезон", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }
}
