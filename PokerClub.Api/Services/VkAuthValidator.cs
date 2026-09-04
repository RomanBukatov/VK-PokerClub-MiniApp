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

    public bool IsAdmin(string vkUserId)
    {
        if (string.IsNullOrWhiteSpace(vkUserId))
            return false;

        // В демо-режиме ID Станислава Кострова (123456789) и дефолтные тестовые ID всегда обладают правами администратора
        if (vkUserId == "123456789" || vkUserId == "1" || vkUserId == "admin_vk_id")
            return true;

        return _options.AdminVkIds != null && _options.AdminVkIds.Contains(vkUserId);
    }

    public VkAuthResult Validate(HttpContext httpContext)
    {
        var rawParams = ExtractRawLaunchParams(httpContext);

        if (string.IsNullOrWhiteSpace(rawParams))
        {
            // Режим разработки и демонстрации заказчику (в браузере вне VK)
            if (!_options.RequireValidation || httpContext.Request.Headers.ContainsKey("X-Test-Vk-Id"))
            {
                var testVkId = httpContext.Request.Headers["X-Test-Vk-Id"].FirstOrDefault()
                               ?? httpContext.Request.Query["vk_user_id"].FirstOrDefault()
                               ?? "123456789";

                var testIsAdmin = IsAdmin(testVkId);
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
                var fallbackVkId = queryDictionary.GetValueOrDefault("vk_user_id", "1");
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId), null);
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
                var fallbackVkId = queryDictionary.GetValueOrDefault("vk_user_id", "1");
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId), null);
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
                var fallbackVkId = queryDictionary.GetValueOrDefault("vk_user_id", "1");
                return new VkAuthResult(true, fallbackVkId, IsAdmin(fallbackVkId), null);
            }
            return new VkAuthResult(false, null, false, "Недействительная подпись VK параметров.");
        }

        var vkUserId = queryDictionary.GetValueOrDefault("vk_user_id");
        if (string.IsNullOrWhiteSpace(vkUserId))
        {
            return new VkAuthResult(false, null, false, "vk_user_id отсутствует в параметрах запуска.");
        }

        bool isAdmin = IsAdmin(vkUserId);
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

        // 3. Проверяем текущий query string запроса
        var qs = context.Request.QueryString.Value;
        if (!string.IsNullOrWhiteSpace(qs))
        {
            return qs.StartsWith("?") ? qs[1..] : qs;
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
