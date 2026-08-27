using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;

namespace PokerClub.Infrastructure.Services;

public class CityService : ICityService
{
    private readonly AppDbContext _context;

    public CityService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<City>> GetActiveCitiesAsync()
    {
        return await _context.Cities
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Clubs.Where(cl => cl.IsActive))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<City?> GetCityByIdAsync(int id)
    {
        return await _context.Cities
            .AsNoTracking()
            .Include(c => c.Clubs.Where(cl => cl.IsActive))
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<City?> GetCityBySlugAsync(string slug)
    {
        return await _context.Cities
            .AsNoTracking()
            .Include(c => c.Clubs.Where(cl => cl.IsActive))
            .FirstOrDefaultAsync(c => c.Slug == slug);
    }
}
