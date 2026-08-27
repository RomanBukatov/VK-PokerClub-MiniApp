namespace PokerClub.Api.Models;

public class VkOptions
{
    public const string SectionName = "VkOptions";

    public long AppId { get; set; }
    public string ClientSecret { get; set; } = string.Empty;
    public List<string> AdminVkIds { get; set; } = new();
    public bool RequireValidation { get; set; } = true;
}
