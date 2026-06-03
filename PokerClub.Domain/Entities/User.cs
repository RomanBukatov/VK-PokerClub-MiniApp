namespace PokerClub.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public required string VkId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public int TotalRating { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}