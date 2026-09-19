import { describe, it, expect } from 'bun:test';
import { getZoneStyle, getLeaderboardSubtitle } from './LeaderboardPanel';

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
