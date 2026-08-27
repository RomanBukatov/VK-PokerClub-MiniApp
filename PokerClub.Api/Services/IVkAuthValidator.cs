namespace PokerClub.Api.Services;

public record VkAuthResult(bool IsValid, string? VkUserId, bool IsAdmin, string? ErrorMessage);

public interface IVkAuthValidator
{
    VkAuthResult Validate(HttpContext httpContext);
    bool IsAdmin(string vkUserId);
}
