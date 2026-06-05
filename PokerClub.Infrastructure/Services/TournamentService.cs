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
            .Include(t => t.Club)
            .ThenInclude(c => c!.City)
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

    public async Task<(bool Success, string Message)> RegisterPlayerAsync(int tournamentId, string vkId)
    {
        // 1. Ищем турнир
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
            user = new User { VkId = vkId, FirstName = "Player", LastName = "VK" }; // Данные обновим с фронта позже
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // 3. Проверка на двойную запись
        if (tournament.Registrations.Any(r => r.UserId == user.Id && r.Status == RegStatus.Active))
            return (false, "Бро, ты уже записан на этот турнир!");

        // 4. Проверка свободных мест
        var activeRegsCount = tournament.Registrations.Count(r => r.Status == RegStatus.Active);
        if (activeRegsCount >= tournament.MaxSeats)
            return (false, "Мест нет! Регистрация полная.");

        // 5. Все ок, записываем
        var registration = new Registration
        {
            TournamentId = tournamentId,
            UserId = user.Id,
            Status = RegStatus.Active
        };

        _context.Registrations.Add(registration);
        await _context.SaveChangesAsync();

        return (true, "Успешно записан! Ждем за столом.");
    }
}