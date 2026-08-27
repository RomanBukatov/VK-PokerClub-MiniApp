using PokerClub.Domain.Entities;

namespace PokerClub.Domain.Interfaces;

public interface ICityService
{
    Task<List<City>> GetActiveCitiesAsync();
    Task<City?> GetCityByIdAsync(int id);
    Task<City?> GetCityBySlugAsync(string slug);
}
