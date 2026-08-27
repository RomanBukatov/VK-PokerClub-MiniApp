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

        return null;
    }

    public static bool IsVkAdmin(this HttpContext context)
    {
        if (context.Items.TryGetValue(IsAdminItemKey, out var val) && val is bool isAdmin)
        {
            return isAdmin;
        }

        return false;
    }
}
