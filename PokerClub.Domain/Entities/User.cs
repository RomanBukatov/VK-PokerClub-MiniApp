namespace PokerClub.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public required string VkId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public int SeasonRating { get; set; } = 0;
    public int TotalRating { get; set; } = 0;
    public string? Nickname { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ClubCardId { get; set; }
    public DateTime? AcceptedTermsAt { get; set; }

    public int TournamentsPlayed { get; set; } = 0;
    public int WinsCount { get; set; } = 0;
    public int Top3Count { get; set; } = 0;
    public int Top10Count { get; set; } = 0;
    public int KnockoutsCount { get; set; } = 0;
    public double AvgPlace { get; set; } = 0.0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}