namespace PokerClub.Domain.Services;

public record RankDefinition(
    int Level,
    string Name,
    int MinRating,
    string Icon
);

public record RankProgressDto(
    int Level,
    string Name,
    string DisplayName,
    string Icon,
    int MinRating,
    string? NextRankName,
    int PointsToNext,
    int ProgressPercent,
    bool IsPrestige,
    int PrestigeMultiplier
);

public static class RankService
{
    public static readonly IReadOnlyList<RankDefinition> Ranks = new List<RankDefinition>
    {
        new(1,  "Новичок", 0, "🎟️"),
        new(2,  "Игрок", 300, "🎲"),
        new(3,  "Претендент", 600, "🃏"),
        new(4,  "Регуляр", 800, "♣️"),
        new(5,  "Тактик", 1100, "♟️"),
        new(6,  "Стратег", 1400, "🫀"),
        new(7,  "Профи", 1800, "♠️"),
        new(8,  "Эксперт", 2100, "🎩"),
        new(9,  "Мастер", 2600, "🎯"),
        new(10, "Грандмастер", 3100, "🏆"),
        new(11, "Элита", 3600, "💎"),
        new(12, "Легенда", 5000, "👑"),
        new(13, "Чемпион", 6500, "🔱"),
        new(14, "Титан", 10000, "⚡"),
        new(15, "Икона Монте-Карло", 15000, "⚓")
    };

    public static string CalculateClubStatus(int rating)
    {
        if (rating > 15000)
        {
            var multiplier = ((rating - 1) / 15000) + 1;
            return $"Икона МК x{multiplier}";
        }

        for (int i = Ranks.Count - 1; i >= 0; i--)
        {
            if (rating >= Ranks[i].MinRating)
            {
                return Ranks[i].Name;
            }
        }

        return "Новичок";
    }

    public static RankProgressDto GetRankProgress(int rating)
    {
        if (rating < 0) rating = 0;

        if (rating > 15000)
        {
            int multiplier = ((rating - 1) / 15000) + 1;
            string displayName = $"Икона МК x{multiplier}";
            int basePoints = (multiplier - 1) * 15000;
            int nextTarget = multiplier * 15000;
            int pointsToNext = nextTarget - rating;
            int progressPercent = Math.Clamp((int)Math.Round(((double)(rating - basePoints) / 15000) * 100), 0, 100);

            return new RankProgressDto(
                Level: 15,
                Name: "Икона Монте-Карло",
                DisplayName: displayName,
                Icon: "⚓",
                MinRating: 15000,
                NextRankName: $"Икона МК x{multiplier + 1}",
                PointsToNext: pointsToNext,
                ProgressPercent: progressPercent,
                IsPrestige: true,
                PrestigeMultiplier: multiplier
            );
        }

        if (rating == 15000)
        {
            return new RankProgressDto(
                Level: 15,
                Name: "Икона Монте-Карло",
                DisplayName: "Икона Монте-Карло",
                Icon: "⚓",
                MinRating: 15000,
                NextRankName: "Икона МК x2",
                PointsToNext: 15000,
                ProgressPercent: 0,
                IsPrestige: false,
                PrestigeMultiplier: 1
            );
        }

        int currentIndex = 0;
        for (int i = Ranks.Count - 1; i >= 0; i--)
        {
            if (rating >= Ranks[i].MinRating)
            {
                currentIndex = i;
                break;
            }
        }

        var current = Ranks[currentIndex];
        var next = currentIndex < Ranks.Count - 1 ? Ranks[currentIndex + 1] : null;

        int targetPoints = next != null ? next.MinRating : 15000;
        int prevPoints = current.MinRating;
        int pointsToNextRank = Math.Max(0, targetPoints - rating);
        int percent = targetPoints > prevPoints 
            ? Math.Clamp((int)Math.Round(((double)(rating - prevPoints) / (targetPoints - prevPoints)) * 100), 0, 100)
            : 0;

        return new RankProgressDto(
            Level: current.Level,
            Name: current.Name,
            DisplayName: current.Name,
            Icon: current.Icon,
            MinRating: current.MinRating,
            NextRankName: next?.Name,
            PointsToNext: pointsToNextRank,
            ProgressPercent: percent,
            IsPrestige: false,
            PrestigeMultiplier: 1
        );
    }
}
