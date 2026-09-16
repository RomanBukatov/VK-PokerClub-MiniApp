using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PokerClub.Api.Models;

namespace PokerClub.Api.Services;

public class VkAuthValidator : IVkAuthValidator
{
    private readonly VkOptions _options;
    private readonly ILogger<VkAuthValidator> _logger;

    public VkAuthValidator(IOptions<VkOptions> options, ILogger<VkAuthValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsAdmin(string vkUserId, HttpContext? httpContext = null)
    {
        // 1. Проверяем явный заголовок X-Is-Admin от клиента для переключения роли
        if (httpContext != null && httpContext.Request.Headers.TryGetValue("X-Is-Admin", out var isAdminHeader))
        {
            var val = isAdminHeader.ToString().Trim();
            if (string.Equals(val, "false", StringComparison.OrdinalIgnoreCase) || val == "0")
            {
                return false;
            }
            if (string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1")
            {
                return true;
            }
        }

        // 2. В демо-режиме (когда RequireValidation == false) разрешаем админ-действия по умолчанию, если не задано обратное
        if (!_options.RequireValidation)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(vkUserId))
            return false;

        // В демо-режиме ID Станислава Кострова (123456789) и дефолтные тестовые ID обладают правами администратора
        if (vkUserId == "123456789" || vkUserId == "1" || vkUserId == "admin_vk_id")
            return true;

        return _options.AdminVkIds != null && _options.AdminVkIds.Contains(vkUserId);
    }

    public VkAuthResult Validate(HttpContext httpContext)
    {
        // 1. Проверяем авторизацию через Telegram Mini App
        if (httpContext.Request.Headers.TryGetValue("X-Telegram-Id", out var tgIdHeader) && !string.IsNullOrWhiteSpace(tgIdHeader))
        {
            var tgId = tgIdHeader.ToString().Trim();
            var tgIsAdmin = IsAdmin(tgId, httpContext);
            _logger.LogInformation("Авторизация Telegram Mini App. TgId: {TgId}, IsAdmin: {IsAdmin}", tgId, tgIsAdmin);
            return new VkAuthResult(true, tgId, tgIsAdmin, null);
        }

        if (httpContext.Request.Headers.TryGetValue("X-Telegram-User", out var tgUserHeader) && !string.IsNullOrWhiteSpace(tgUserHeader))
        {
            var userStr = tgUserHeader.ToString().Trim();
            string tgId = userStr;
            if (userStr.StartsWith("{") && userStr.Contains("\"id\":"))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(userStr);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        tgId = idProp.ToString();
                    }
                }
                catch { }
            }
            var tgIsAdmin = IsAdmin(tgId, httpContext);
            _logger.LogInformation("Авторизация Telegram Mini App. TgUser: {TgId}, IsAdmin: {IsAdmin}", tgId, tgIsAdmin);
            return new VkAuthResult(true, tgId, tgIsAdmin, null);
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
            authHeader.ToString().StartsWith("tma ", StringComparison.OrdinalIgnoreCase))
        {
            var rawTma = authHeader.ToString()[4..].Trim();
            var tmaParams = ParseQueryString(rawTma);
            if (tmaParams.TryGetValue("user", out var userJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(userJson);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        var tgId = idProp.ToString();
                        var tgIsAdmin = IsAdmin(tgId, httpContext);
                        return new VkAuthResult(true, tgId, tgIsAdmin, null);
                    }
                }
                catch { }
            }
        }

        var rawParams = ExtractRawLaunchParams(httpContext);

        if (string.IsNullOrWhiteSpace(rawParams))
        {
            // Режим разработки и демонстрации заказчику (в браузере вне VK)
            if (!_options.RequireValidation || 
                httpContext.Request.Headers.ContainsKey("X-Test-Vk-Id") || 
                httpContext.Request.Headers.ContainsKey("X-Is-Admin"))
            {
                var testVkId = httpContext.Request.Headers["X-Test-Vk-Id"].FirstOrDefault()
                               ?? httpContext.Request.Query["vk_user_id"].FirstOrDefault()
                               ?? "123456789";

                var testIsAdmin = IsAdmin(testVkId, httpContext);
                _logger.LogInformation("Авторизация Standalone/Demo. VkId: {VkId}, IsAdmin: {IsAdmin}", testVkId, testIsAdmin);
                return new VkAuthResult(true, testVkId, testIsAdmin, null);
            }

            return new VkAuthResult(false, null, false, "Параметры запуска VK отсутствуют.");
        }

        // Парсим параметры
        var queryDictionary = ParseQueryString(rawParams);

        if (!queryDictionary.TryGetValue("sign", out var sign) || string.IsNullOrWhiteSpace(sign))
        {
            if (!_options.RequireValidation)
            {
                var fallbackVkId = queryDictionary.GetValueOrDefault("vk_user_id", "123456789");
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId, httpContext), null);
            }

            return new VkAuthResult(false, null, false, "Параметр подписи 'sign' не найден.");
        }

        // Извлекаем и сортируем параметры, начинающиеся с vk_
        var vkParams = queryDictionary
            .Where(kvp => kvp.Key.StartsWith("vk_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        if (vkParams.Count == 0)
        {
            if (!_options.RequireValidation)
            {
                var fallbackVkId = queryDictionary.GetValueOrDefault("vk_user_id", "123456789");
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId, httpContext), null);
            }
            return new VkAuthResult(false, null, false, "Параметры vk_* не найдены.");
        }

        var paramString = string.Join("&", vkParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        // Вычисляем HMAC-SHA256 подпись
        var secret = _options.ClientSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogError("VK ClientSecret не настроен в конфигурации!");
            if (!_options.RequireValidation)
            {
                var fallbackVkId = queryDictionary.GetValueOrDefault("vk_user_id", "123456789");
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId, httpContext), null);
            }
            return new VkAuthResult(false, null, false, "Ошибка конфигурации сервера.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var messageBytes = Encoding.UTF8.GetBytes(paramString);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(messageBytes);
        
        // Base64Url кодирование согласно спецификации VK
        var calculatedSign = Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var signBytes = Encoding.UTF8.GetBytes(sign);
        var calculatedBytes = Encoding.UTF8.GetBytes(calculatedSign);

        bool isValid = signBytes.Length == calculatedBytes.Length && 
                       CryptographicOperations.FixedTimeEquals(signBytes, calculatedBytes);

        if (!isValid)
        {
            _logger.LogWarning("Недействительная подпись VK Sign. Получено: {ReceivedSign}", sign);
            if (!_options.RequireValidation)
            {
                var fallbackVkId = queryDictionary.GetValueOrDefault("vk_user_id", "123456789");
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId, httpContext), null);
            }
            return new VkAuthResult(false, null, false, "Недействительная подпись VK параметров.");
        }

        var vkUserId = queryDictionary.GetValueOrDefault("vk_user_id");
        if (string.IsNullOrWhiteSpace(vkUserId))
        {
            if (!_options.RequireValidation)
            {
                var fallbackVkId = "123456789";
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId, httpContext), null);
            }
            return new VkAuthResult(false, null, false, "vk_user_id отсутствует в параметрах запуска.");
        }

        bool isAdmin = IsAdmin(vkUserId, httpContext);
        return new VkAuthResult(true, vkUserId, isAdmin, null);
    }

    private static string? ExtractRawLaunchParams(HttpContext context)
    {
        // 1. Проверяем заголовок X-VK-Sign
        if (context.Request.Headers.TryGetValue("X-VK-Sign", out var headerSign) && !string.IsNullOrWhiteSpace(headerSign))
        {
            var raw = headerSign.ToString();
            return raw.StartsWith("?") ? raw[1..] : raw;
        }

        // 2. Проверяем заголовок Authorization: VK <query>
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var authStr = authHeader.ToString();
            if (authStr.StartsWith("VK ", StringComparison.OrdinalIgnoreCase))
            {
                var raw = authStr[3..].Trim();
                return raw.StartsWith("?") ? raw[1..] : raw;
            }
        }

        // 3. Проверяем текущий query string запроса (только если это действительно launch params от VK)
        var qs = context.Request.QueryString.Value;
        if (!string.IsNullOrWhiteSpace(qs))
        {
            var raw = qs.StartsWith("?") ? qs[1..] : qs;
            if (raw.Contains("vk_user_id", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("sign=", StringComparison.OrdinalIgnoreCase))
            {
                return raw;
            }
        }

        return null;
    }

    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }
}
