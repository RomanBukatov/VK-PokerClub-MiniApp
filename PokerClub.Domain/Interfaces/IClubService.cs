using PokerClub.Domain.Entities;

namespace PokerClub.Domain.Interfaces;

public interface IClubService
{
    Task<List<Club>> GetClubsAsync(int? cityId = null);
    Task<Club?> GetClubByIdAsync(int id);
}
