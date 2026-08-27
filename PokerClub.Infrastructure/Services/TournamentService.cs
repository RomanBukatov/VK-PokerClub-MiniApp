using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;

namespace PokerClub.Infrastructure.Services;

public class TournamentService : ITournamentService
{
    private readonly AppDbContext _context;

    public TournamentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Tournament>> GetScheduleAsync(int? cityId, int? clubId)
    {
        var query = _context.Tournaments
            .AsNoTracking()
            .Include(t => t.Club)
                .ThenInclude(c => c!.City)
            .Include(t => t.Registrations)
            .Where(t => t.Status == TournamentStatus.Announced || t.Status == TournamentStatus.RegistrationOpen);

        if (clubId.HasValue)
        {
            query = query.Where(t => t.ClubId == clubId.Value);
        }
        else if (cityId.HasValue)
        {
            query = query.Where(t => t.Club!.CityId == cityId.Value);
        }

        return await query.OrderBy(t => t.StartTime).ToListAsync();
    }

    public async Task<Tournament?> GetTournamentByIdAsync(int id)
    {
        return await _context.Tournaments
            .AsNoTracking()
            .Include(t => t.Club)
                .ThenInclude(c => c!.City)
            .Include(t => t.Registrations.Where(r => r.Status == RegStatus.Active))
                .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<Tournament>> GetUserTournamentsAsync(string vkId)
    {
        return await _context.Tournaments
            .AsNoTracking()
            .Include(t => t.Club)
                .ThenInclude(c => c!.City)
            .Include(t => t.Registrations)
            .Where(t => t.Registrations.Any(r => r.User!.VkId == vkId && r.Status != RegStatus.Canceled))
            .OrderByDescending(t => t.StartTime)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message)> RegisterPlayerAsync(int tournamentId, string vkId)
    {
        // 1. Ищем турнир с регистрациями
        var tournament = await _context.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament == null)
            return (false, "Турнир не найден.");

        if (tournament.Status != TournamentStatus.RegistrationOpen)
            return (false, "Регистрация на этот турнир закрыта или еще не началась.");

        // 2. Ищем или создаем юзера (т.к. это VK Mini App, он может зайти впервые)
        var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == vkId);
        if (user == null)
        {
            user = new User
            {
                VkId = vkId,
                FirstName = "Игрок",
                LastName = "VK"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // 3. Проверяем существующую регистрацию
        var existingReg = tournament.Registrations.FirstOrDefault(r => r.UserId == user.Id);
        if (existingReg != null)
        {
            if (existingReg.Status == RegStatus.Active)
                return (false, "Вы уже записаны на этот турнир.");

            if (existingReg.Status == RegStatus.Played)
                return (false, "Вы уже приняли участие в данном турнире.");

            // Статус был Canceled - реактивируем с проверкой мест
            var activeCountForReactivation = tournament.Registrations.Count(r => r.Status == RegStatus.Active);
            if (activeCountForReactivation >= tournament.MaxSeats)
                return (false, "Свободных мест больше нет. Регистрация закрыта.");

            existingReg.Status = RegStatus.Active;
            await _context.SaveChangesAsync();
            return (true, "Запись успешно восстановлена! Ждем вас за столом.");
        }

        // 4. Проверка свободных мест для нового участника
        var activeCount = tournament.Registrations.Count(r => r.Status == RegStatus.Active);
        if (activeCount >= tournament.MaxSeats)
            return (false, "Свободных мест больше нет. Регистрация закрыта.");

        // 5. Записываем нового игрока
        var registration = new Registration
        {
            TournamentId = tournamentId,
            UserId = user.Id,
            Status = RegStatus.Active
        };

        _context.Registrations.Add(registration);
        await _context.SaveChangesAsync();

        return (true, "Вы успешно записаны на турнир! Ждем вас за столом.");
    }

    public async Task<(bool Success, string Message)> CancelRegistrationAsync(int tournamentId, string vkId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.VkId == vkId);
        if (user == null)
            return (false, "Пользователь не найден.");

        var tournament = await _context.Tournaments
            .Include(t => t.Registrations)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament == null)
            return (false, "Турнир не найден.");

        if (tournament.Status == TournamentStatus.Finished || tournament.Status == TournamentStatus.Canceled)
            return (false, "Невозможно отменить запись на завершенный или отмененный турнир.");

        var registration = tournament.Registrations.FirstOrDefault(r => r.UserId == user.Id);
        if (registration == null || registration.Status != RegStatus.Active)
            return (false, "Активная запись на данный турнир не найдена.");

        registration.Status = RegStatus.Canceled;
        await _context.SaveChangesAsync();

        return (true, "Запись на турнир успешно отменена.");
    }
}