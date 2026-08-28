using Microsoft.EntityFrameworkCore;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;

namespace PokerClub.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context)
    {
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

        // 3. Топ-25 игроков с реалистичными очками
        var usersData = new (string VkId, string FirstName, string LastName, int TotalRating)[]
        {
            ("1001", "Алексей", "Крылов", 2840),
            ("123456789", "Станислав", "Костров", 2450), // Тестовый пользователь / Админ
            ("1002", "Андрей", "Смирнов", 2180),
            ("1003", "Сергей", "Волков", 1950),
            ("1004", "Дмитрий", "Морозов", 1820),
            ("1005", "Иван", "Попов", 1710),
            ("1006", "Максим", "Новиков", 1630),
            ("1007", "Артем", "Соколов", 1520),
            ("1008", "Михаил", "Васильев", 1440),
            ("1009", "Павел", "Кузнецов", 1380),
            ("1010", "Роман", "Лебедев", 1250),
            ("1011", "Денис", "Козлов", 1160),
            ("1012", "Егор", "Павлов", 1080),
            ("1013", "Антон", "Семенов", 990),
            ("1014", "Илья", "Голубев", 920),
            ("1015", "Кирилл", "Богданов", 840),
            ("1016", "Никита", "Воробьев", 760),
            ("1017", "Владислав", "Федоров", 680),
            ("1018", "Ярослав", "Михайлов", 610),
            ("1019", "Глеб", "Беляев", 540),
            ("1020", "Константин", "Тарасов", 470),
            ("1021", "Вадим", "Медведев", 390),
            ("1022", "Олег", "Казаков", 320),
            ("1023", "Виктор", "Савельев", 230),
            ("1024", "Тимофей", "Виноградов", 150),
        };

        var users = new List<User>();
        foreach (var u in usersData)
        {
            var user = new User
            {
                VkId = u.VkId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                TotalRating = u.TotalRating,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            users.Add(user);
        }
        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

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
            StartTime = today19.AddDays(1).AddHours(1), // Завтра 20:00
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
            StartTime = today19.AddDays(3).AddHours(-1), // Воскресенье 18:00
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
            StartTime = DateTime.UtcNow.AddDays(-1).Date.AddHours(19), // Вчера 19:00
            Status = TournamentStatus.Finished,
            Description = "Еженедельный кубок клуба. Очки начислены в рейтинг.",
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        };

        await context.Tournaments.AddRangeAsync(tour1, tour2, tour3, tour4);
        await context.SaveChangesAsync();

        // 5. Записи игроков (Registrations)
        var registrations = new List<Registration>();
        var otherUsers = users.Where(u => u.VkId != "123456789").ToList();

        // 18 игроков на Freeroll Tournament (без тестового пользователя Станислава Кострова)
        for (int i = 0; i < 18 && i < otherUsers.Count; i++)
        {
            registrations.Add(new Registration
            {
                TournamentId = tour1.Id,
                UserId = otherUsers[i].Id,
                Status = RegStatus.Active,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }

        // 5 игроков на DeepStack
        for (int i = 0; i < 5 && i < otherUsers.Count; i++)
        {
            registrations.Add(new Registration
            {
                TournamentId = tour2.Id,
                UserId = otherUsers[i].Id,
                Status = RegStatus.Active,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }

        // 25 игроков на Sunday Grand (из 30 мест — статус «Осталось мало мест»)
        for (int i = 0; i < 25 && i < otherUsers.Count; i++)
        {
            registrations.Add(new Registration
            {
                TournamentId = tour3.Id,
                UserId = otherUsers[i].Id,
                Status = RegStatus.Active,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }

        // 20 игроков на завершенный Weekly Club Cup с начисленными очками (включая тестового пользователя с 70 очками)
        int[] cupPoints = { 100, 70, 50, 35, 20, 10, 10, 10, 10, 10, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 };
        for (int i = 0; i < 20 && i < users.Count; i++)
        {
            registrations.Add(new Registration
            {
                TournamentId = tour4.Id,
                UserId = users[i].Id,
                Status = RegStatus.Played,
                PointsEarned = i < cupPoints.Length ? cupPoints[i] : 5,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            });
        }

        await context.Registrations.AddRangeAsync(registrations);
        await context.SaveChangesAsync();
    }
}
