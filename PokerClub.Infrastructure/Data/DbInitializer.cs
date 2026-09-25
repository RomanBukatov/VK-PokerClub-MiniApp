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
        if (user.VkId == "123456789" || user.VkId.StartsWith("sheet_", StringComparison.OrdinalIgnoreCase) || user.VkId.StartsWith("card_", StringComparison.OrdinalIgnoreCase))
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
        await EnsureDatabaseSchemaAsync(context);

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

        // Очистка испорченных ClubCardId для всех sheet_* пользователей,
        // у которых ClubCardId совпадает с их рейтинговым местом (SheetRank) или числом сыгранных турниров
        var corruptedSheetUsers = allUsers.Where(u =>
            u.VkId.StartsWith("sheet_", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(u.ClubCardId) &&
            ((u.SheetRank.HasValue && string.Equals(u.ClubCardId.Trim(), u.SheetRank.Value.ToString(), StringComparison.OrdinalIgnoreCase)) ||
             (u.TournamentsPlayed > 0 && string.Equals(u.ClubCardId.Trim(), u.TournamentsPlayed.ToString(), StringComparison.OrdinalIgnoreCase)))
        ).ToList();

        if (corruptedSheetUsers.Count > 0)
        {
            foreach (var u in corruptedSheetUsers)
            {
                u.ClubCardId = null;
            }
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

        // Гарантируем актуальный адрес клуба Monte Carlo
        var monteCarloClub = await context.Clubs.FirstOrDefaultAsync(c => c.Name == "Monte Carlo");
        if (monteCarloClub != null && (string.IsNullOrWhiteSpace(monteCarloClub.Address) ||
            monteCarloClub.Address.Contains("Монастырск", StringComparison.OrdinalIgnoreCase)))
        {
            monteCarloClub.Address = "ул. Куйбышева, 7, кафе «Гости»";
            await context.SaveChangesAsync();
        }

        // Гарантируем DEFAULT 0 для колонки TotalRating и наличие StartingStack в PostgreSQL
        if (context.Database.IsRelational())
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Tournaments\" ADD COLUMN IF NOT EXISTS \"StartingStack\" integer NOT NULL DEFAULT 10000;");
            }
            catch (Exception)
            {
                // логирование или игнор если уже есть
            }

            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ALTER COLUMN \"TotalRating\" SET DEFAULT 0;");
            }
            catch
            {
            }

            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"AcceptedTermsAt\" timestamp with time zone NULL;");
            }
            catch
            {
            }

            try
            {
                await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"SheetRank\" integer NULL;");
            }
            catch
            {
            }
        }
    }

    public static async Task EnsureDatabaseSchemaAsync(AppDbContext context)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Tournaments\" ADD COLUMN IF NOT EXISTS \"StartingStack\" integer NOT NULL DEFAULT 10000;");
        }
        catch (Exception)
        {
            // Игнорируем, если таблица еще не создана
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ALTER COLUMN \"TotalRating\" SET DEFAULT 0;");
        }
        catch
        {
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"AcceptedTermsAt\" timestamp with time zone NULL;");
        }
        catch
        {
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"SheetRank\" integer NULL;");
        }
        catch (Exception)
        {
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                SELECT '20260923163546_AddStartingStackToTournament', '10.0.8'
                WHERE EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Tournaments' AND column_name = 'StartingStack'
                )
                ON CONFLICT (""MigrationId"") DO NOTHING;");
        }
        catch
        {
        }

        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                SELECT '20260924170000_AddSheetRankToUser', '10.0.8'
                WHERE EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Users' AND column_name = 'SheetRank'
                )
                ON CONFLICT (""MigrationId"") DO NOTHING;");
        }
        catch
        {
        }
    }

    public static async Task CleanFakeTournamentsAsync(AppDbContext context)
    {
        await EnsureDatabaseSchemaAsync(context);

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
            Address = "ул. Куйбышева, 7, кафе «Гости»",
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
