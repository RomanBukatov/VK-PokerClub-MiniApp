import { describe, it, expect } from 'bun:test';
import { RANKS, getRankProgress, getClubRankName } from './ranks.config';

describe('ranks.config', () => {
  it('should have exactly 15 ranks ordered from level 1 to level 15', () => {
    expect(RANKS.length).toBe(15);
    expect(RANKS[0].level).toBe(1);
    expect(RANKS[0].name).toBe('Новичок');
    expect(RANKS[0].icon).toBe('🎟️');

    expect(RANKS[14].level).toBe(15);
    expect(RANKS[14].name).toBe('Икона Монте-Карло');
    expect(RANKS[14].icon).toBe('⚓');
  });

  it('should return correct rank names via getClubRankName', () => {
    expect(getClubRankName(0)).toBe('Новичок');
    expect(getClubRankName(100)).toBe('Новичок');
    expect(getClubRankName(299)).toBe('Новичок');
    expect(getClubRankName(300)).toBe('Игрок');
    expect(getClubRankName(599)).toBe('Игрок');
    expect(getClubRankName(600)).toBe('Претендент');
    expect(getClubRankName(800)).toBe('Регуляр');
    expect(getClubRankName(1100)).toBe('Тактик');
    expect(getClubRankName(1400)).toBe('Стратег');
    expect(getClubRankName(1800)).toBe('Профи');
    expect(getClubRankName(2100)).toBe('Эксперт');
    expect(getClubRankName(2600)).toBe('Мастер');
    expect(getClubRankName(3100)).toBe('Грандмастер');
    expect(getClubRankName(3600)).toBe('Элита');
    expect(getClubRankName(5000)).toBe('Легенда');
    expect(getClubRankName(6500)).toBe('Чемпион');
    expect(getClubRankName(10000)).toBe('Титан');
    expect(getClubRankName(15000)).toBe('Икона Монте-Карло');
    expect(getClubRankName(15001)).toBe('Икона МК x2');
    expect(getClubRankName(30000)).toBe('Икона МК x2');
    expect(getClubRankName(30001)).toBe('Икона МК x3');
    expect(getClubRankName(45001)).toBe('Икона МК x4');
  });

  it('should match the customer specification: «Текущий уровень: Профи ➔ До ранга Эксперт осталось 300 очков»', () => {
    const progress = getRankProgress(1800);
    expect(progress.displayName).toBe('Профи');
    expect(progress.badgeText).toBe('7 LVL');
    expect(progress.nextRank.name).toBe('Эксперт');
    expect(progress.pointsToNext).toBe(300);
    expect(progress.progressPercent).toBe(0);
  });

  it('should calculate progress for Новичок at 0 points', () => {
    const progress = getRankProgress(0);
    expect(progress.displayName).toBe('Новичок');
    expect(progress.badgeText).toBe('1 LVL');
    expect(progress.nextRank.name).toBe('Игрок');
    expect(progress.pointsToNext).toBe(300);
    expect(progress.progressPercent).toBe(0);
  });

  it('should calculate midway progress from 1800 to 2100', () => {
    const progress = getRankProgress(1950);
    expect(progress.displayName).toBe('Профи');
    expect(progress.nextRank.name).toBe('Эксперт');
    expect(progress.pointsToNext).toBe(150);
    expect(progress.progressPercent).toBe(50);
  });

  it('should handle exactly 15000 points (Икона Монте-Карло)', () => {
    const progress = getRankProgress(15000);
    expect(progress.displayName).toBe('Икона Монте-Карло');
    expect(progress.badgeText).toBe('15 LVL');
    expect(progress.nextRank.name).toBe('Икона МК x2');
    expect(progress.pointsToNext).toBe(15000);
    expect(progress.progressPercent).toBe(0);
    expect(progress.isPrestige).toBe(false);
  });

  it('should handle prestige x2 for points > 15000', () => {
    const progress = getRankProgress(16500);
    expect(progress.displayName).toBe('Икона МК x2');
    expect(progress.badgeText).toBe('x2');
    expect(progress.nextRank.name).toBe('Икона МК x3');
    expect(progress.pointsToNext).toBe(13500);
    expect(progress.progressPercent).toBe(10);
    expect(progress.isPrestige).toBe(true);
    expect(progress.prestigeMultiplier).toBe(2);
  });

  it('should handle prestige x3 for points > 30000', () => {
    const progress = getRankProgress(35000);
    expect(progress.displayName).toBe('Икона МК x3');
    expect(progress.badgeText).toBe('x3');
    expect(progress.nextRank.name).toBe('Икона МК x4');
    expect(progress.pointsToNext).toBe(10000);
    expect(progress.progressPercent).toBe(33);
    expect(progress.isPrestige).toBe(true);
    expect(progress.prestigeMultiplier).toBe(3);
  });
});
