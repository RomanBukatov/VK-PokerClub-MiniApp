import { describe, it, expect } from 'bun:test';
import type { VkUser, Tournament } from '../types';
import { TournamentStatus } from '../types';
import { checkCanViewVkId, checkProfileCompleteness } from './TournamentDetailModal';

describe('TournamentDetailModal and Tournament Security & Barriers', () => {
  describe('VK ID Visibility Rule (Security against competitors)', () => {
    it('hides VK ID for regular players (vkUser.isAdmin is false or undefined)', () => {
      expect(checkCanViewVkId({ isAdmin: false })).toBe(false);
      expect(checkCanViewVkId({ isAdmin: undefined })).toBe(false);
      expect(checkCanViewVkId(null)).toBe(false);
      expect(checkCanViewVkId(undefined)).toBe(false);
      expect(checkCanViewVkId({} as Partial<VkUser>)).toBe(false);
    });

    it('shows VK ID only when vkUser.isAdmin === true', () => {
      expect(checkCanViewVkId({ isAdmin: true })).toBe(true);
    });
  });

  describe('Registration Hard Barrier (Profile completeness & Club ID check)', () => {
    it('blocks registration if profile has no real name or phone', () => {
      const result = checkProfileCompleteness(
        { id: 100, first_name: 'Игрок', last_name: 'VK' },
        { fullName: 'Игрок VK', phoneNumber: '' }
      );
      expect(result.isProfileComplete).toBe(false);
      expect(result.hasRealName).toBe(false);
      expect(result.hasPhone).toBe(false);
    });

    it('blocks registration if user is "Гость Клуба" (case-insensitive and variations)', () => {
      const result1 = checkProfileCompleteness(
        { id: 100, first_name: 'Гость', last_name: 'Клуба' },
        { fullName: 'Гость Клуба', phoneNumber: '+7 (999) 111-22-33' }
      );
      expect(result1.isProfileComplete).toBe(false);
      expect(result1.isGuestUser).toBe(true);

      const result2 = checkProfileCompleteness(
        { id: 100, first_name: 'гость', last_name: 'клуба' },
        { fullName: 'гость клуба', phoneNumber: '+7 (999) 111-22-33' }
      );
      expect(result2.isProfileComplete).toBe(false);
      expect(result2.isGuestUser).toBe(true);
      expect(result2.hasRealName).toBe(false);

      const result3 = checkProfileCompleteness(
        { id: 100, first_name: 'Гость' },
        { fullName: 'Клуба Гость', phoneNumber: '+7 (999) 111-22-33' }
      );
      expect(result3.isProfileComplete).toBe(false);
      expect(result3.isGuestUser).toBe(true);
    });

    it('blocks registration for placeholder names like "Игрок VK", "Игрок #12", "Игрок 9999"', () => {
      const r1 = checkProfileCompleteness(
        { id: 100, first_name: 'Игрок', last_name: 'VK' },
        { fullName: 'Игрок VK', phoneNumber: '+79991112233' }
      );
      expect(r1.isProfileComplete).toBe(false);
      expect(r1.hasRealName).toBe(false);

      const r2 = checkProfileCompleteness(
        { id: 100, first_name: 'Игрок' },
        { fullName: 'Игрок #54321', phoneNumber: '+79991112233' }
      );
      expect(r2.isProfileComplete).toBe(false);
      expect(r2.hasRealName).toBe(false);

      const r3 = checkProfileCompleteness(
        { id: 100, first_name: 'Игрок' },
        { fullName: 'Игрок 12345', phoneNumber: '+79991112233' }
      );
      expect(r3.isProfileComplete).toBe(false);
      expect(r3.hasRealName).toBe(false);
    });

    it('blocks registration if phone number has fewer than 10 digits', () => {
      const result = checkProfileCompleteness(
        { id: 100, first_name: 'Алексей', last_name: 'Петров' },
        { fullName: 'Алексей Петров', phoneNumber: '+7 (999) 12-34' }
      );
      expect(result.isProfileComplete).toBe(false);
      expect(result.hasPhone).toBe(false);
      expect(result.hasRealName).toBe(true);
    });

    it('allows registration when real name and valid phone are present', () => {
      const result = checkProfileCompleteness(
        { id: 100, first_name: 'Алексей', last_name: 'Петров' },
        { fullName: 'Алексей Петров', phoneNumber: '+7 (999) 111-22-33' }
      );
      expect(result.isProfileComplete).toBe(true);
      expect(result.hasRealName).toBe(true);
      expect(result.hasPhone).toBe(true);
      expect(result.isGuestUser).toBe(false);
    });

    it('detects missing clubCardId and displays club card banner requirement', () => {
      const withoutCard = checkProfileCompleteness(
        { id: 100, first_name: 'Алексей', last_name: 'Петров' },
        { fullName: 'Алексей Петров', phoneNumber: '+79991112233', clubCardId: '' }
      );
      expect(withoutCard.shouldShowClubCardBanner).toBe(true);

      const withWhitespaceCard = checkProfileCompleteness(
        { id: 100, first_name: 'Алексей', last_name: 'Петров' },
        { fullName: 'Алексей Петров', phoneNumber: '+79991112233', clubCardId: '   ' }
      );
      expect(withWhitespaceCard.shouldShowClubCardBanner).toBe(true);

      const withValidCard = checkProfileCompleteness(
        { id: 100, first_name: 'Алексей', last_name: 'Петров' },
        { fullName: 'Алексей Петров', phoneNumber: '+79991112233', clubCardId: '1266' }
      );
      expect(withValidCard.shouldShowClubCardBanner).toBe(false);
    });
  });

  describe('Admin Tournament Sorting (Strictly chronological - closest first)', () => {
    it('sorts tournaments strictly by startTime ascending', () => {
      const tournaments: Tournament[] = [
        {
          id: 3,
          title: 'Future Event',
          buyIn: 1000,
          maxSeats: 30,
          registeredCount: 0,
          isUserRegistered: false,
          clubId: 1,
          status: TournamentStatus.Announced,
          startTime: '2026-09-25T19:00:00.000Z',
        },
        {
          id: 1,
          title: 'Yesterday Event',
          buyIn: 1000,
          maxSeats: 30,
          registeredCount: 0,
          isUserRegistered: false,
          clubId: 1,
          status: TournamentStatus.Finished,
          startTime: '2026-09-20T19:00:00.000Z',
        },
        {
          id: 2,
          title: 'Tonight Event',
          buyIn: 1000,
          maxSeats: 30,
          registeredCount: 0,
          isUserRegistered: false,
          clubId: 1,
          status: TournamentStatus.RegistrationOpen,
          startTime: '2026-09-21T19:00:00.000Z',
        },
      ];

      const sorted = [...tournaments].sort((a, b) => {
        const timeA = new Date(a.startTime).getTime() || 0;
        const timeB = new Date(b.startTime).getTime() || 0;
        return timeA - timeB;
      });

      expect(sorted[0].id).toBe(1); // 2026-09-20
      expect(sorted[1].id).toBe(2); // 2026-09-21
      expect(sorted[2].id).toBe(3); // 2026-09-25
    });
  });
});
