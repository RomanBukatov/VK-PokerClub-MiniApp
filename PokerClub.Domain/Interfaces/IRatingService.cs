using PokerClub.Domain.Entities;

namespace PokerClub.Domain.Interfaces;

public interface IRatingService
{
    // Получить лидерборд с пагинацией
    Task<(List<User> Users, int TotalCount)> GetLeaderboardAsync(int limit = 50, int offset = 0, string type = "season");
    
    // Админский метод: начислить очки и завершить турнир
    Task<(bool Success, string Message)> AssignPointsAndFinishTournamentAsync(int tournamentId, Dictionary<int, int> userPoints);
}