using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;

namespace PokerClub.Infrastructure.Services;

public class RatingService : IRatingService
{
    private readonly AppDbContext _context;

    public RatingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetLeaderboardAsync(int limit = 50)
    {
        return await _context.Users
            .Where(u => u.TotalRating > 0)
            .OrderByDescending(u => u.TotalRating)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> AssignPointsAndFinishTournamentAsync(int tournamentId, Dictionary<int, int> userPoints)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament == null)
            return (false, "Турнир не найден.");

        if (tournament.Status == TournamentStatus.Finished)
            return (false, "Турнир уже завершен, бро. Очки уже начислены.");

        foreach (var reg in tournament.Registrations)
        {
            if (userPoints.TryGetValue(reg.UserId, out int points))
            {
                reg.PointsEarned = points;
                reg.Status = RegStatus.Played;
            }
        }

        tournament.Status = TournamentStatus.Finished;
        await _context.SaveChangesAsync();

        var userIdsToUpdate = userPoints.Keys.ToList();
        var users = await _context.Users.Where(u => userIdsToUpdate.Contains(u.Id)).ToListAsync();

        foreach (var user in users)
        {
            var total = await _context.Registrations
                .Where(r => r.UserId == user.Id && r.Status == RegStatus.Played)
                .SumAsync(r => r.PointsEarned);

            user.TotalRating = total;
        }

        await _context.SaveChangesAsync();

        return (true, "Турнир завершен, рейтинг пацанам пересчитан!");
    }
}