using PokerClub.Domain.Entities;

namespace PokerClub.Domain.Interfaces;

public interface ITournamentService
{
    // Получить расписание (активные турниры) по городу/клубу
    Task<List<Tournament>> GetScheduleAsync(int? cityId, int? clubId);
    
    // Запись на турнир (с проверками)
    Task<(bool Success, string Message)> RegisterPlayerAsync(int tournamentId, string vkId);
}