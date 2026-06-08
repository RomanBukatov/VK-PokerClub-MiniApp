using PokerClub.Domain.Entities;

namespace PokerClub.Domain.Interfaces;

public interface IRatingService
{
    // Получить лидерборд
    Task<List<User>> GetLeaderboardAsync(int limit = 50);
    
    // Админский метод: начислить очки и завершить турнир
    Task<(bool Success, string Message)> AssignPointsAndFinishTournamentAsync(int tournamentId, Dictionary<int, int> userPoints);
}