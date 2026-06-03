using PokerClub.Domain.Enums;

namespace PokerClub.Domain.Entities;

public class Registration
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public int UserId { get; set; }
    public RegStatus Status { get; set; } = RegStatus.Active;
    public int PointsEarned { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tournament? Tournament { get; set; }
    public User? User { get; set; }
}