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
            .AsNoTracking()
            .Where(u => u.TotalRating > 0)
            .OrderByDescending(u => u.TotalRating)
            .ThenBy(u => u.Id)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> AssignPointsAndFinishTournamentAsync(
        int tournamentId, 
        Dictionary<int, int> userPoints)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var tournament = await _context.Tournaments
                .Include(t => t.Registrations)
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null)
                return (false, "Турнир не найден.");

            // 1. Проставляем/обновляем очки и статус сыграно для участников
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

            // 2. Оптимизированный расчет рейтинга без N+1 запросов:
            // Получаем список ID пользователей для обновления
            var userIdsToUpdate = userPoints.Keys.Distinct().ToList();
            if (userIdsToUpdate.Count > 0)
            {
                // Считаем суммарные очки для всех затронутых пользователей одним запросом
                var totalsByUser = await _context.Registrations
                    .Where(r => userIdsToUpdate.Contains(r.UserId) && r.Status == RegStatus.Played)
                    .GroupBy(r => r.UserId)
                    .Select(g => new { UserId = g.Key, TotalRating = g.Sum(r => r.PointsEarned) })
                    .ToDictionaryAsync(x => x.UserId, x => x.TotalRating);

                var users = await _context.Users
                    .Where(u => userIdsToUpdate.Contains(u.Id))
                    .ToListAsync();

                foreach (var user in users)
                {
                    user.TotalRating = totalsByUser.TryGetValue(user.Id, out int total) ? total : 0;
                }

                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            return (true, "Турнир успешно завершен, рейтинг игроков обновлен.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, $"Ошибка при завершении турнира: {ex.Message}");
        }
    }
}