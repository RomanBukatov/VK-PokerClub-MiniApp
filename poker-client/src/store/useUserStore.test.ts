import { describe, it, expect, beforeEach } from 'bun:test';
import { useUserStore } from './useUserStore';
import { useTournamentsStore } from './useTournamentsStore';
import { usersApi } from '../api/usersApi';

describe('useUserStore logout and reset', () => {
  beforeEach(() => {
    // Mock localStorage
    const store: Record<string, string> = {
      poker_legal_accepted: 'true',
      poker_accepted_terms_at: '2026-09-17T12:00:00Z',
      poker_profile_completed: 'true',
      poker_is_admin: 'true',
      vk_test_user_id: '999999',
      tg_user_id: '888888',
      poker_custom_setting: 'test',
    };

    const mockStorage: Storage = {
      getItem: (k: string) => store[k] ?? null,
      setItem: (k: string, v: string) => { store[k] = v; },
      removeItem: (k: string) => { delete store[k]; },
      clear: () => {
        Object.keys(store).forEach((k) => delete store[k]);
      },
      get length() { return Object.keys(store).length; },
      key: (i: number) => Object.keys(store)[i] ?? null,
    };

    Object.defineProperty(globalThis, 'window', {
      value: {
        location: { search: '' },
        localStorage: mockStorage,
      },
      writable: true,
      configurable: true,
    });

    Object.defineProperty(globalThis, 'localStorage', {
      value: mockStorage,
      writable: true,
      configurable: true,
    });
  });

  it('should reset user state and clear user localStorage keys on logout / resetUser', () => {
    // Set initial logged in state
    useUserStore.setState({
      vkUser: { id: 123, first_name: 'Test', last_name: 'User' },
      profile: {
        id: 1,
        vkId: '123',
        totalRating: 500,
        status: 'Pro',
        tournamentsPlayed: 10,
        winsCount: 2,
        top3Count: 3,
        top10Count: 5,
        knockoutsCount: 15,
        avgPlace: 3.5,
        createdAt: '2026-01-01',
      },
      isAuthenticated: true,
      isAdmin: true,
      activeTab: 'admin-tournaments',
      isLegalModalOpen: false,
    });

    useTournamentsStore.setState({
      myTournaments: [{ id: 1, title: 'Tournament', buyIn: 100, maxSeats: 10, startTime: '2026-01-01', status: 1, clubId: 1, registeredCount: 1, isUserRegistered: true }],
      selectedTournament: null,
      isDetailModalOpen: true,
      actionError: 'some error',
      scheduleError: 'network error',
    });

    expect(useUserStore.getState().isAuthenticated).toBe(true);
    expect(useUserStore.getState().isAdmin).toBe(true);
    expect(useTournamentsStore.getState().myTournaments.length).toBe(1);

    // Call logout
    useUserStore.getState().logout();

    const stateAfter = useUserStore.getState();
    expect(stateAfter.vkUser).toBeNull();
    expect(stateAfter.profile).toBeNull();
    expect(stateAfter.isAuthenticated).toBe(false);
    expect(stateAfter.isAdmin).toBe(false);
    expect(stateAfter.activeTab).toBe('schedule');
    expect(stateAfter.isLegalModalOpen).toBe(true);
    expect(stateAfter.isProfileModalOpen).toBe(false);

    // Verify localStorage keys were removed
    expect(window.localStorage.getItem('poker_legal_accepted')).toBeNull();
    expect(window.localStorage.getItem('poker_accepted_terms_at')).toBeNull();
    expect(window.localStorage.getItem('poker_profile_completed')).toBeNull();
    expect(window.localStorage.getItem('poker_is_admin')).toBeNull();
    expect(window.localStorage.getItem('vk_test_user_id')).toBeNull();
    expect(window.localStorage.getItem('tg_user_id')).toBeNull();
    expect(window.localStorage.getItem('poker_custom_setting')).toBeNull();

    // Verify tournaments store cleanup
    const tourneysAfter = useTournamentsStore.getState();
    expect(tourneysAfter.myTournaments.length).toBe(0);
    expect(tourneysAfter.isDetailModalOpen).toBe(false);
    expect(tourneysAfter.actionError).toBeNull();
    expect(tourneysAfter.scheduleError).toBeNull();
  });

  it('should restore vkUser and set isAuthenticated upon updateProfile post-logout', async () => {
    // Start from logged out state
    useUserStore.getState().logout();
    expect(useUserStore.getState().vkUser).toBeNull();
    expect(useUserStore.getState().isAuthenticated).toBe(false);

    // Mock usersApi.updateProfile
    const originalUpdate = usersApi.updateProfile;
    usersApi.updateProfile = async () => ({
      id: 42,
      vkId: '98765',
      firstName: 'Алексей',
      lastName: 'Смирнов',
      nickname: 'AlexAce',
      phoneNumber: '+7 (999) 123-45-67',
      totalRating: 100,
      status: 'Newbie',
      tournamentsPlayed: 0,
      winsCount: 0,
      top3Count: 0,
      top10Count: 0,
      knockoutsCount: 0,
      avgPlace: 0,
      createdAt: '2026-09-17',
    });

    try {
      await useUserStore.getState().updateProfile({
        nickname: 'AlexAce',
        phoneNumber: '+7 (999) 123-45-67',
      });

      const state = useUserStore.getState();
      expect(state.isAuthenticated).toBe(true);
      expect(state.profile?.nickname).toBe('AlexAce');
      expect(state.vkUser).not.toBeNull();
      expect(state.vkUser?.id).toBe(98765);
      expect(state.vkUser?.first_name).toBe('Алексей');
      expect(window.localStorage.getItem('poker_profile_completed')).toBe('true');
      expect(window.localStorage.getItem('vk_test_user_id')).toBe('98765');
    } finally {
      usersApi.updateProfile = originalUpdate;
    }
  });

  it('should handle acceptTerms and transition to profile modal when profile is incomplete', async () => {
    useUserStore.getState().logout();

    const originalAccept = usersApi.acceptTerms;
    usersApi.acceptTerms = async () => ({
      success: true,
      acceptedTermsAt: '2026-09-17T15:00:00Z',
      message: 'OK',
    });

    try {
      await useUserStore.getState().acceptTerms();

      const state = useUserStore.getState();
      expect(state.isLegalModalOpen).toBe(false);
      expect(state.isProfileModalOpen).toBe(true);
      expect(window.localStorage.getItem('poker_legal_accepted')).toBe('true');
    } finally {
      usersApi.acceptTerms = originalAccept;
    }
  });

  it('should handle 401 error in fetchProfile gracefully, reset isLoading to false, and allow guest access', async () => {
    useUserStore.getState().logout();
    expect(useUserStore.getState().isLoading).toBe(false);

    const originalGetMe = usersApi.getMe;
    usersApi.getMe = async () => {
      const err = new Error('Request failed with status code 401');
      (err as unknown as { response: { status: number } }).response = { status: 401 };
      throw err;
    };

    try {
      const res = await useUserStore.getState().fetchProfile();
      expect(res).toBeNull();

      const state = useUserStore.getState();
      expect(state.isLoading).toBe(false);
      expect(state.isLoadingProfile).toBe(false);
      expect(state.isAuthenticated).toBe(true);
      expect(state.activeTab).toBe('schedule');
    } finally {
      usersApi.getMe = originalGetMe;
    }
  });

  it('should execute getMe and reset isLoading in finally', async () => {
    const originalGetMe = usersApi.getMe;
    usersApi.getMe = async () => ({
      id: 10,
      vkId: '555',
      totalRating: 200,
      status: 'Fish',
      tournamentsPlayed: 1,
      winsCount: 0,
      top3Count: 0,
      top10Count: 1,
      knockoutsCount: 2,
      avgPlace: 4.0,
      createdAt: '2026-09-17',
    });

    try {
      const profile = await useUserStore.getState().getMe();
      expect(profile?.id).toBe(10);
      expect(useUserStore.getState().isLoading).toBe(false);
    } finally {
      usersApi.getMe = originalGetMe;
    }
  });

  it('should handle initUser with provided user and reset isLoading in finally', async () => {
    await useUserStore.getState().initUser({
      id: 999,
      first_name: 'Иван',
      last_name: 'Тестов',
    });

    const state = useUserStore.getState();
    expect(state.vkUser?.id).toBe(999);
    expect(state.isAuthenticated).toBe(true);
    expect(state.isLoading).toBe(false);
  });
});
