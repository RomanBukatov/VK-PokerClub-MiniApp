import { describe, it, expect } from 'bun:test';
import { getZoneStyle, getLeaderboardSubtitle, getDisplayRank, getUserRankLabel } from './LeaderboardPanel';

describe('getZoneStyle', () => {
  it('assigns "Финал" (emerald) for ranks 1 to 70', () => {
    const rank1 = getZoneStyle(1);
    expect(rank1.badge).toBe('Финал');
    expect(rank1.cardBg).toContain('emerald');
    expect(rank1.rankColor).toContain('emerald');

    const rank70 = getZoneStyle(70);
    expect(rank70.badge).toBe('Финал');
    expect(rank70.cardBg).toContain('emerald');
  });

  it('assigns "Претендент" (amber) for ranks 71 to 100', () => {
    const rank71 = getZoneStyle(71);
    expect(rank71.badge).toBe('Претендент');
    expect(rank71.cardBg).toContain('amber');
    expect(rank71.rankColor).toContain('amber');

    const rank100 = getZoneStyle(100);
    expect(rank100.badge).toBe('Претендент');
    expect(rank100.cardBg).toContain('amber');
  });

  it('assigns "Риск" (rose) for ranks 101 to 120', () => {
    const rank101 = getZoneStyle(101);
    expect(rank101.badge).toBe('Риск');
    expect(rank101.cardBg).toContain('rose');
    expect(rank101.rankColor).toContain('rose');

    const rank120 = getZoneStyle(120);
    expect(rank120.badge).toBe('Риск');
    expect(rank120.cardBg).toContain('rose');
  });

  it('assigns neutral style without badge for ranks 121 and above', () => {
    const rank121 = getZoneStyle(121);
    expect(rank121.badge).toBeNull();
    expect(rank121.cardBg).toContain('black');

    const rank500 = getZoneStyle(500);
    expect(rank500.badge).toBeNull();
    expect(rank500.cardBg).toContain('black');
  });
});

describe('getLeaderboardSubtitle', () => {
  it('returns all-time title when seasonTab is "all" or "all-time"', () => {
    expect(getLeaderboardSubtitle('all')).toBe('Общий зачет клуба · Зал славы');
    expect(getLeaderboardSubtitle('all-time')).toBe('Общий зачет клуба · Зал славы');
  });

  it('returns season name when seasonTab is "current"', () => {
    expect(getLeaderboardSubtitle('current', 'Осень 2026')).toBe('Сезон: Осень 2026');
    expect(getLeaderboardSubtitle('current')).toBe('Сезон: Осень 2026');
  });
});

describe('getDisplayRank', () => {
  it('returns sheetRank when sheetRank is defined', () => {
    expect(getDisplayRank({ rank: 1, sheetRank: 157 })).toBe(157);
    expect(getDisplayRank({ rank: 5, sheetRank: 2 })).toBe(2);
  });

  it('falls back to rank when sheetRank is null or undefined', () => {
    expect(getDisplayRank({ rank: 1, sheetRank: null })).toBe(1);
    expect(getDisplayRank({ rank: 42 })).toBe(42);
    expect(getDisplayRank({ rank: 10, sheetRank: undefined })).toBe(10);
  });
});

describe('getUserRankLabel', () => {
  it('formats rank when user has points and rank', () => {
    expect(getUserRankLabel(157, 10, 50)).toBe('# 157');
    expect(getUserRankLabel(1, 486, 50)).toBe('# 1');
  });

  it('returns "Не в рейтинге" when user has 0 points even if rank exists', () => {
    expect(getUserRankLabel(157, 0, 50)).toBe('Не в рейтинге');
    expect(getUserRankLabel(null, 0, 50)).toBe('Не в рейтинге');
  });

  it('returns club size overflow when user has points but rank is null', () => {
    expect(getUserRankLabel(null, 15, 50)).toBe('50+ в клубе');
    expect(getUserRankLabel(null, 15, 0)).toBe('50+ в клубе');
    expect(getUserRankLabel(null, 15, 75)).toBe('75+ в клубе');
  });
});
