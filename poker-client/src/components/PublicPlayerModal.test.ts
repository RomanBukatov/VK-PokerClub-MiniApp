import { describe, it, expect } from 'bun:test';
import { getAchievements } from '../config/achievements.config';
import type { Achievement } from '../types';

describe('PublicPlayerModal logic and formatting', () => {
  describe('Club card badge logic', () => {
    const formatCardBadge = (clubCardId?: string | null): string | null => {
      if (!clubCardId || !clubCardId.trim()) return null;
      const trimmed = clubCardId.trim();
      return trimmed.startsWith('#') ? trimmed : `#${trimmed}`;
    };

    it('returns null for empty or whitespace card IDs', () => {
      expect(formatCardBadge(null)).toBeNull();
      expect(formatCardBadge(undefined)).toBeNull();
      expect(formatCardBadge('')).toBeNull();
      expect(formatCardBadge('   ')).toBeNull();
    });

    it('formats card numbers with leading hash without duplicating', () => {
      expect(formatCardBadge('MC-777')).toBe('#MC-777');
      expect(formatCardBadge('#MC-777')).toBe('#MC-777');
      expect(formatCardBadge('  12345  ')).toBe('#12345');
    });
  });

  describe('Phone link and tel: URI sanitization', () => {
    const getTelUri = (phoneNumber?: string | null): string | null => {
      if (!phoneNumber || !phoneNumber.trim()) return null;
      return `tel:${phoneNumber.replace(/[^\d+]/g, '')}`;
    };

    it('returns null for empty or whitespace phone numbers', () => {
      expect(getTelUri(null)).toBeNull();
      expect(getTelUri(undefined)).toBeNull();
      expect(getTelUri('')).toBeNull();
      expect(getTelUri('   ')).toBeNull();
    });

    it('removes spaces, parentheses, and dashes from tel: URI to prevent mobile webview crashes', () => {
      expect(getTelUri('+7 (999) 123-45-67')).toBe('tel:+79991234567');
      expect(getTelUri('8 999 123 45 67')).toBe('tel:89991234567');
      expect(getTelUri('+79991234567')).toBe('tel:+79991234567');
    });
  });

  describe('VK link and sheet user detection', () => {
    const getVkTarget = (vkId?: string | null): { isLinked: boolean; url?: string } => {
      const clean = vkId?.trim();
      if (!clean || clean.startsWith('sheet_')) {
        return { isLinked: false };
      }
      return { isLinked: true, url: `https://vk.com/im?sel=${clean}` };
    };

    it('detects sheet_ accounts as unlinked VK', () => {
      expect(getVkTarget('sheet_12345')).toEqual({ isLinked: false });
      expect(getVkTarget('sheet_vasya')).toEqual({ isLinked: false });
      expect(getVkTarget(null)).toEqual({ isLinked: false });
      expect(getVkTarget('')).toEqual({ isLinked: false });
      expect(getVkTarget('   ')).toEqual({ isLinked: false });
    });

    it('generates direct VK messenger link for real users', () => {
      expect(getVkTarget('123456789')).toEqual({
        isLinked: true,
        url: 'https://vk.com/im?sel=123456789',
      });
      expect(getVkTarget('  durov  ')).toEqual({
        isLinked: true,
        url: 'https://vk.com/im?sel=durov',
      });
    });
  });

  describe('Achievements grid (12 achievements)', () => {
    it('returns exactly 12 achievements', () => {
      const achievements = getAchievements({});
      expect(achievements).toHaveLength(12);
    });

    it('locks all achievements for a brand new player with 0 stats', () => {
      const achievements = getAchievements({
        tournamentsPlayed: 0,
        winsCount: 0,
        top3Count: 0,
        knockoutsCount: 0,
        totalRating: 0,
      });

      expect(achievements.every((a: Achievement) => !a.isUnlocked)).toBe(true);
      expect(achievements.filter((a: Achievement) => a.isUnlocked).length).toBe(0);
    });

    it('correctly unlocks tier-1 achievements', () => {
      const achievements = getAchievements({
        tournamentsPlayed: 1,
        winsCount: 1,
        top3Count: 3,
        knockoutsCount: 0,
        totalRating: 0,
      });

      const firstStep = achievements.find((a: Achievement) => a.id === 'first_step')!;
      const firstWin = achievements.find((a: Achievement) => a.id === 'first_win')!;
      const onFire = achievements.find((a: Achievement) => a.id === 'on_fire')!;

      expect(firstStep.isUnlocked).toBe(true);
      expect(firstStep.progressPercent).toBe(100);
      expect(firstStep.current).toBe(1);

      expect(firstWin.isUnlocked).toBe(true);
      expect(firstWin.progressPercent).toBe(100);
      expect(firstWin.current).toBe(1);

      expect(onFire.isUnlocked).toBe(true);
      expect(onFire.progressPercent).toBe(100);
      expect(onFire.current).toBe(3);
    });

    it('correctly unlocks regular and elite achievements', () => {
      // Regular player with 500 rating, 25 tournaments, 2 wins, 25 knockouts
      const regularAchievements = getAchievements({
        tournamentsPlayed: 25,
        winsCount: 2,
        top3Count: 5,
        knockoutsCount: 25,
        totalRating: 500,
      });

      const veteran = regularAchievements.find((a: Achievement) => a.id === 'veteran')!;
      const bountyHunter = regularAchievements.find((a: Achievement) => a.id === 'bounty_hunter')!;
      const shark = regularAchievements.find((a: Achievement) => a.id === 'shark')!;
      const champion = regularAchievements.find((a: Achievement) => a.id === 'champion')!;
      const grinder = regularAchievements.find((a: Achievement) => a.id === 'grinder')!;
      const executioner = regularAchievements.find((a: Achievement) => a.id === 'executioner')!;
      const highRoller = regularAchievements.find((a: Achievement) => a.id === 'high_roller')!;
      const legend = regularAchievements.find((a: Achievement) => a.id === 'legend')!;

      expect(veteran.isUnlocked).toBe(true); // 25 >= 15
      expect(bountyHunter.isUnlocked).toBe(true); // 25 >= 20
      expect(shark.isUnlocked).toBe(true); // 500 >= 300
      expect(champion.isUnlocked).toBe(false); // 2 < 3
      expect(grinder.isUnlocked).toBe(false); // 25 < 30
      expect(executioner.isUnlocked).toBe(false); // 25 < 50
      expect(highRoller.isUnlocked).toBe(false); // 500 < 600
      expect(legend.isUnlocked).toBe(false); // 500 < 1000

      // Elite player with 1050 rating, 35 tournaments, 5 wins, 55 knockouts
      const eliteAchievements = getAchievements({
        tournamentsPlayed: 35,
        winsCount: 5,
        top3Count: 10,
        knockoutsCount: 55,
        totalRating: 1050,
      });

      expect(eliteAchievements.every((a: Achievement) => a.isUnlocked)).toBe(true);
      expect(eliteAchievements.filter((a: Achievement) => a.isUnlocked).length).toBe(12);
    });

    it('caps current value and progress percent at target/100%', () => {
      const achievements = getAchievements({
        tournamentsPlayed: 100,
        winsCount: 20,
        top3Count: 30,
        knockoutsCount: 200,
        totalRating: 5000,
      });

      for (const ach of achievements) {
        expect(ach.current).toBe(ach.target);
        expect(ach.progressPercent).toBe(100);
        expect(ach.isUnlocked).toBe(true);
      }
    });

    it('safely handles negative stats without negative progress or values', () => {
      const achievements = getAchievements({
        tournamentsPlayed: -5,
        winsCount: -1,
        top3Count: -2,
        knockoutsCount: -10,
        totalRating: -50,
      });

      expect(achievements.every((a: Achievement) => !a.isUnlocked)).toBe(true);
      expect(achievements.every((a: Achievement) => a.current >= 0)).toBe(true);
      expect(achievements.every((a: Achievement) => a.progressPercent >= 0)).toBe(true);
    });
  });
});
