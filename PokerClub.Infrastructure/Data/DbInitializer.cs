using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;

namespace PokerClub.Infrastructure.Data;

public static class DbInitializer
{
    public static bool IsFakeBot(User user)
    {
        if (string.IsNullOrWhiteSpace(user.VkId))
        {
            return false;
        }

        // Не удаляем тестового администратора и реальных игроков из Google Sheets
        if (user.VkId == "123456789" || user.VkId.StartsWith("sheet_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Боты с VkId типа "user1".."user7"
        if (user.VkId.StartsWith("user", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Боты из сида DbInitializer с VkId "1001".."1024"
        if (int.TryParse(user.VkId, out var id) && id >= 1001 && id <= 1024)
        {
            return true;
        }

        return false;
    }

    public static async Task CleanFakeUsersAsync(AppDbContext context)
    {
        var allUsers = await context.Users.ToListAsync();
        var fakeUsers = allUsers.Where(IsFakeBot).ToList();

        if (fakeUsers.Count > 0)
        {
            var fakeUserIds = fakeUsers.Select(u => u.Id).ToList();
            var fakeRegs = await context.Registrations
                .Where(r => fakeUserIds.Contains(r.UserId))
                .ToListAsync();

            if (fakeRegs.Count > 0)
            {
                context.Registrations.RemoveRange(fakeRegs);
            }

            context.Users.RemoveRange(fakeUsers);
            await context.SaveChangesAsync();
        }

        // Тестовый администратор: гарантируем рейтинг 0, чтобы не перекрывать реальных лидеров
        var admin = await context.Users.FirstOrDefaultAsync(u => u.VkId == "123456789");
        if (admin != null && (admin.TotalRating > 0 || admin.SeasonRating > 0))
        {
            admin.TotalRating = 0;
            admin.SeasonRating = 0;
            await context.SaveChangesAsync();
        }

        // Гарантируем DEFAULT 0 для колонки TotalRating в PostgreSQL
        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ALTER COLUMN \"TotalRating\" SET DEFAULT 0;");
        }
        catch
        {
            // Игнорируем, если база не PostgreSQL (например, InMemory в тестах) или дефолт уже применен
        }
    }

    public static async Task CleanFakeTournamentsAsync(AppDbContext context)
    {
        var startDate = new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);

        var fakeTournaments = await context.Tournaments
            .Where(t => (t.Title == "Weekly Club Cup"
                      || t.Title == "Freeroll Tournament"
                      || t.Title == "Texas Holdem DeepStack"
                      || t.Title == "Sunday Grand Event")
                     && t.StartTime >= startDate && t.StartTime < endDate)
            .ToListAsync();

        if (fakeTournaments.Count > 0)
        {
            var fakeTourIds = fakeTournaments.Select(t => t.Id).ToList();
            var relatedRegs = await context.Registrations
                .Where(r => fakeTourIds.Contains(r.TournamentId))
                .ToListAsync();

            if (relatedRegs.Count > 0)
            {
                context.Registrations.RemoveRange(relatedRegs);
            }

            context.Tournaments.RemoveRange(fakeTournaments);
            await context.SaveChangesAsync();
        }
    }

    public static async Task SeedAsync(AppDbContext context)
    {
        // Очищаем тестовых ботов и старые тестовые турниры сидера
        await CleanFakeUsersAsync(context);
        await CleanFakeTournamentsAsync(context);

        if (await context.Cities.AnyAsync())
        {
            return; // База уже заполнена
        }

        // 1. Город
        var perm = new City
        {
            Name = "Пермь",
            Slug = "perm",
            IsActive = true
        };
        await context.Cities.AddAsync(perm);
        await context.SaveChangesAsync();

        // 2. Клуб
        var monteCarlo = new Club
        {
            CityId = perm.Id,
            Name = "Monte Carlo",
            Address = "Монастырская улица, 59, Пермь",
            IsActive = true
        };
        await context.Clubs.AddAsync(monteCarlo);
        await context.SaveChangesAsync();

        // 3. Тестовый пользователь / Админ (без тестовых ботов)
        var admin = await context.Users.FirstOrDefaultAsync(u => u.VkId == "123456789");
        if (admin == null)
        {
            admin = new User
            {
                VkId = "123456789",
                FirstName = "Станислав",
                LastName = "Костров",
                SeasonRating = 0,
                TotalRating = 0,
                Nickname = "MonteCarloBoss",
                PhoneNumber = "+7 (999) 123-45-67",
                ClubCardId = "777",
                AcceptedTermsAt = DateTime.UtcNow.AddDays(-30),
                TournamentsPlayed = 0,
                WinsCount = 0,
                Top3Count = 0,
                Top10Count = 0,
                KnockoutsCount = 0,
                AvgPlace = 0.0,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            await context.Users.AddAsync(admin);
            await context.SaveChangesAsync();
        }
    }
}
