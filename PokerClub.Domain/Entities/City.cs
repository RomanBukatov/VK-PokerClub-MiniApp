namespace PokerClub.Domain.Entities;

public class City
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Club> Clubs { get; set; } = new List<Club>();
}