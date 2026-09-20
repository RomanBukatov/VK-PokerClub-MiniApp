namespace PokerClub.Api.Extensions;

public static class HttpContextExtensions
{
    public const string VkUserIdItemKey = "VkUserId";
    public const string IsAdminItemKey = "IsAdmin";

    public static string? GetVkUserId(this HttpContext? context)
    {
        if (context == null)
        {
            return null;
        }

        if (context.Items.TryGetValue(VkUserIdItemKey, out var val) && val is string vkId)
        {
            return vkId;
        }

        if (context.Request.Headers.TryGetValue("X-Telegram-Id", out var tgId) && !string.IsNullOrWhiteSpace(tgId))
        {
            return tgId.ToString().Trim();
        }

        if (context.Request.Headers.TryGetValue("X-Telegram-User", out var tgUser) && !string.IsNullOrWhiteSpace(tgUser))
        {
            var userStr = tgUser.ToString().Trim();
            // Если передан JSON {"id":123,...}
            if (userStr.StartsWith("{") && userStr.Contains("\"id\":"))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(userStr);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        return idProp.ToString();
                    }
                }
                catch { }
            }
            return userStr;
        }

        if (context.Request.Headers.TryGetValue("X-Test-Vk-Id", out var testId) && !string.IsNullOrWhiteSpace(testId))
        {
            return testId.ToString();
        }

        if (context.Request.Query.TryGetValue("vk_user_id", out var queryVkId) && !string.IsNullOrWhiteSpace(queryVkId))
        {
            return queryVkId.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-VK-Sign", out var signHeader) && !string.IsNullOrWhiteSpace(signHeader))
        {
            var qs = signHeader.ToString();
            if (qs.StartsWith("?")) qs = qs[1..];
            var parsed = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(qs);
            if (parsed.TryGetValue("vk_user_id", out var signVkId) && !string.IsNullOrWhiteSpace(signVkId))
            {
                return signVkId.ToString();
            }
        }

        return null;
    }

    public static bool IsVkAdmin(this HttpContext? context)
    {
        if (context == null)
        {
            return false;
        }

        if (context.Items.TryGetValue(IsAdminItemKey, out var val) && val is bool isAdmin)
        {
            return isAdmin;
        }

        var vkId = context.GetVkUserId();
        if (!string.IsNullOrWhiteSpace(vkId))
        {
            var validator = context.RequestServices?.GetService<Services.IVkAuthValidator>();
            if (validator != null && validator.IsConfiguredAdmin(vkId))
            {
                return true;
            }
        }

        if (context.Request.Headers.TryGetValue("X-Is-Admin", out var adminHeader))
        {
            var valStr = adminHeader.ToString().Trim();
            return string.Equals(valStr, "true", StringComparison.OrdinalIgnoreCase) || valStr == "1";
        }

        return false;
    }
}
