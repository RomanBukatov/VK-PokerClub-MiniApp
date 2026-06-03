namespace PokerClub.Domain.Entities;

public class Club
{
    public int Id { get; set; }
    public int CityId { get; set; }
    public required string Name { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    public City? City { get; set; }
    public ICollection<Tournament> Tournaments { get; set; } = new List<Tournament>();
}