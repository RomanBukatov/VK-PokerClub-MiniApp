using PokerClub.Domain.Services;
using Xunit;

namespace PokerClub.Tests;

public class RankServiceTests
{
    [Theory]
    [InlineData(-10, "Новичок")]
    [InlineData(0, "Новичок")]
    [InlineData(50, "Новичок")]
    [InlineData(100, "Новичок")]
    [InlineData(299, "Новичок")]
    [InlineData(300, "Игрок")]
    [InlineData(599, "Игрок")]
    [InlineData(600, "Претендент")]
    [InlineData(799, "Претендент")]
    [InlineData(800, "Регуляр")]
    [InlineData(1099, "Регуляр")]
    [InlineData(1100, "Тактик")]
    [InlineData(1399, "Тактик")]
    [InlineData(1400, "Стратег")]
    [InlineData(1799, "Стратег")]
    [InlineData(1800, "Профи")]
    [InlineData(2099, "Профи")]
    [InlineData(2100, "Эксперт")]
    [InlineData(2599, "Эксперт")]
    [InlineData(2600, "Мастер")]
    [InlineData(3099, "Мастер")]
    [InlineData(3100, "Грандмастер")]
    [InlineData(3599, "Грандмастер")]
    [InlineData(3600, "Элита")]
    [InlineData(4999, "Элита")]
    [InlineData(5000, "Легенда")]
    [InlineData(6499, "Легенда")]
    [InlineData(6500, "Чемпион")]
    [InlineData(9999, "Чемпион")]
    [InlineData(10000, "Титан")]
    [InlineData(14999, "Титан")]
    [InlineData(15000, "Икона Монте-Карло")]
    [InlineData(15001, "Икона МК x2")]
    [InlineData(18000, "Икона МК x2")]
    [InlineData(30000, "Икона МК x2")]
    [InlineData(30001, "Икона МК x3")]
    [InlineData(45000, "Икона МК x3")]
    [InlineData(45001, "Икона МК x4")]
    public void CalculateClubStatus_ReturnsExpectedRank(int rating, string expectedStatus)
    {
        var status = RankService.CalculateClubStatus(rating);
        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void Ranks_AreOrderedFromLevel1To15()
    {
        Assert.Equal(15, RankService.Ranks.Count);
        for (int i = 0; i < RankService.Ranks.Count; i++)
        {
            Assert.Equal(i + 1, RankService.Ranks[i].Level);
        }
        Assert.Equal("Новичок", RankService.Ranks[0].Name);
        Assert.Equal("Икона Монте-Карло", RankService.Ranks[14].Name);
    }

    [Fact]
    public void GetRankProgress_Pro_Returns300PointsLeftToExpert()
    {
        // «Текущий уровень: Профи ➔ До ранга Эксперт осталось 300 очков»
        var progress = RankService.GetRankProgress(1800);

        Assert.Equal(7, progress.Level);
        Assert.Equal("Профи", progress.Name);
        Assert.Equal("Профи", progress.DisplayName);
        Assert.Equal("Эксперт", progress.NextRankName);
        Assert.Equal(300, progress.PointsToNext);
        Assert.Equal(0, progress.ProgressPercent);
        Assert.Equal("♠️", progress.Icon);
        Assert.False(progress.IsPrestige);
        Assert.Equal(1, progress.PrestigeMultiplier);
    }

    [Fact]
    public void GetRankProgress_Newbie_CalculatesPointsToPlayer()
    {
        var progress = RankService.GetRankProgress(0);

        Assert.Equal(1, progress.Level);
        Assert.Equal("Новичок", progress.Name);
        Assert.Equal("Игрок", progress.NextRankName);
        Assert.Equal(300, progress.PointsToNext);
        Assert.Equal(0, progress.ProgressPercent);
        Assert.Equal("🎟️", progress.Icon);
    }

    [Fact]
    public void GetRankProgress_MidwayProgress_CalculatesCorrectPercent()
    {
        // From 1800 (Профи) to 2100 (Эксперт): halfway is 1950 (150 pts left, 50%)
        var progress = RankService.GetRankProgress(1950);

        Assert.Equal(7, progress.Level);
        Assert.Equal("Профи", progress.Name);
        Assert.Equal("Эксперт", progress.NextRankName);
        Assert.Equal(150, progress.PointsToNext);
        Assert.Equal(50, progress.ProgressPercent);
    }

    [Fact]
    public void GetRankProgress_IconMonteCarlo_Exact15000()
    {
        var progress = RankService.GetRankProgress(15000);

        Assert.Equal(15, progress.Level);
        Assert.Equal("Икона Монте-Карло", progress.Name);
        Assert.Equal("Икона Монте-Карло", progress.DisplayName);
        Assert.Equal("Икона МК x2", progress.NextRankName);
        Assert.Equal(15000, progress.PointsToNext);
        Assert.Equal(0, progress.ProgressPercent);
        Assert.False(progress.IsPrestige);
        Assert.Equal(1, progress.PrestigeMultiplier);
    }

    [Fact]
    public void GetRankProgress_PrestigeX2_CalculatesPointsToX3()
    {
        // 16500 points is in prestige x2 (target: 30000, points left: 13500, progress: 1500/15000 = 10%)
        var progress = RankService.GetRankProgress(16500);

        Assert.Equal(15, progress.Level);
        Assert.True(progress.IsPrestige);
        Assert.Equal(2, progress.PrestigeMultiplier);
        Assert.Equal("Икона МК x2", progress.DisplayName);
        Assert.Equal("Икона МК x3", progress.NextRankName);
        Assert.Equal(13500, progress.PointsToNext);
        Assert.Equal(10, progress.ProgressPercent);
    }

    [Fact]
    public void GetRankProgress_PrestigeX3_CalculatesPointsToX4()
    {
        // 35000 points is in prestige x3 (target: 45000, points left: 10000, progress: 5000/15000 = 33%)
        var progress = RankService.GetRankProgress(35000);

        Assert.Equal(15, progress.Level);
        Assert.True(progress.IsPrestige);
        Assert.Equal(3, progress.PrestigeMultiplier);
        Assert.Equal("Икона МК x3", progress.DisplayName);
        Assert.Equal("Икона МК x4", progress.NextRankName);
        Assert.Equal(10000, progress.PointsToNext);
        Assert.Equal(33, progress.ProgressPercent);
    }
}
