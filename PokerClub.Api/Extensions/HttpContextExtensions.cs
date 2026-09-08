namespace PokerClub.Api.Extensions;

public static class HttpContextExtensions
{
    public const string VkUserIdItemKey = "VkUserId";
    public const string IsAdminItemKey = "IsAdmin";

    public static string? GetVkUserId(this HttpContext context)
    {
        if (context.Items.TryGetValue(VkUserIdItemKey, out var val) && val is string vkId)
        {
            return vkId;
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

    public static bool IsVkAdmin(this HttpContext context)
    {
        if (context.Items.TryGetValue(IsAdminItemKey, out var val) && val is bool isAdmin)
        {
            return isAdmin;
        }

        if (context.Request.Headers.TryGetValue("X-Is-Admin", out var adminHeader))
        {
            var valStr = adminHeader.ToString().Trim();
            return string.Equals(valStr, "true", StringComparison.OrdinalIgnoreCase) || valStr == "1";
        }

        return false;
    }
}
