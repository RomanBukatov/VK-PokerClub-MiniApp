using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;

namespace PokerClub.Infrastructure.Services;

public class ClubService : IClubService
{
    private readonly AppDbContext _context;

    public ClubService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Club>> GetClubsAsync(int? cityId = null)
    {
        var query = _context.Clubs
            .AsNoTracking()
            .Include(c => c.City)
            .Where(c => c.IsActive);

        if (cityId.HasValue)
        {
            query = query.Where(c => c.CityId == cityId.Value);
        }

        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Club?> GetClubByIdAsync(int id)
    {
        return await _context.Clubs
            .AsNoTracking()
            .Include(c => c.City)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
    }
}
