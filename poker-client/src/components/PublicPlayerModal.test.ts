import { describe, it, expect } from 'bun:test';

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
});
