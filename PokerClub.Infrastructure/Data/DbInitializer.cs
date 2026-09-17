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
    }

    public static async Task SeedAsync(AppDbContext context)
    {
        // Очищаем тестовых ботов
        await CleanFakeUsersAsync(context);

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

        // 4. Турниры
        var now = DateTime.UtcNow;
        var today19 = DateTime.UtcNow.Date.AddHours(19);
        if (today19 < now) today19 = today19.AddDays(1);

        var tour1 = new Tournament
        {
            ClubId = monteCarlo.Id,
            Title = "Freeroll Tournament",
            Format = "NL Holdem",
            BuyIn = 0,
            MaxSeats = 30,
            StartTime = today19,
            Status = TournamentStatus.RegistrationOpen,
            Description = "Бесплатный регулярный турнир для участников клуба.\n\n• Стартовый стек — 10 000 фишек\n• Блайнд-апы — 15 минут\n• Поздняя регистрация — 3 часа\n\nRe-Buy: 20 000 — 1000 ₽\nAdd-on: 40 000 — 1000 ₽",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var tour2 = new Tournament
        {
            ClubId = monteCarlo.Id,
            Title = "Texas Holdem DeepStack",
            Format = "DeepStack",
            BuyIn = 1500,
            MaxSeats = 40,
            StartTime = today19.AddDays(1).AddHours(1),
            Status = TournamentStatus.Announced,
            Description = "Глубокая структура со стартовым стеком 25 000 фишек. Уровни по 20 минут.",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var tour3 = new Tournament
        {
            ClubId = monteCarlo.Id,
            Title = "Sunday Grand Event",
            Format = "NL Holdem",
            BuyIn = 3000,
            MaxSeats = 30,
            StartTime = today19.AddDays(3).AddHours(-1),
            Status = TournamentStatus.RegistrationOpen,
            Description = "Главное событие недели с повышенным рейтингом. Стартовый стек 50 000 фишек.",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        var tour4 = new Tournament
        {
            ClubId = monteCarlo.Id,
            Title = "Weekly Club Cup",
            Format = "6-Max Turbo",
            BuyIn = 1000,
            MaxSeats = 20,
            StartTime = DateTime.UtcNow.AddDays(-1).Date.AddHours(19),
            Status = TournamentStatus.Finished,
            Description = "Еженедельный кубок клуба. Очки начислены в рейтинг.",
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        };

        await context.Tournaments.AddRangeAsync(tour1, tour2, tour3, tour4);
        await context.SaveChangesAsync();
    }
}
