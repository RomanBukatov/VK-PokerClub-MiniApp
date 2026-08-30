using PokerClub.Domain.Entities;

namespace PokerClub.Domain.Interfaces;

public interface ITournamentService
{
    // Получить расписание (активные турниры) по городу/клубу
    Task<List<Tournament>> GetScheduleAsync(int? cityId, int? clubId, bool includeFinished = false);
    
    // Получить детальную информацию о турнире
    Task<Tournament?> GetTournamentByIdAsync(int id);

    // Получить турниры, на которые записан пользователь
    Task<List<Tournament>> GetUserTournamentsAsync(string vkId);
    
    // Запись на турнир (с проверками)
    Task<(bool Success, string Message)> RegisterPlayerAsync(int tournamentId, string vkId);

    // Отмена записи на турнир
    Task<(bool Success, string Message)> CancelRegistrationAsync(int tournamentId, string vkId);

    // Создание нового турнира (админ)
    Task<(bool Success, Tournament? Tournament, string Message)> CreateTournamentAsync(
        int clubId,
        string title,
        string? format,
        decimal buyIn,
        int maxSeats,
        DateTime startTime,
        string? description
    );
}