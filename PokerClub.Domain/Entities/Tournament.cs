using PokerClub.Domain.Enums;

namespace PokerClub.Domain.Entities;

public class Tournament
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public required string Title { get; set; }
    public string? Format { get; set; }
    public decimal BuyIn { get; set; }
    public string? Description { get; set; }
    public int MaxSeats { get; set; }
    public DateTime StartTime { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Announced;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Club? Club { get; set; }
    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}