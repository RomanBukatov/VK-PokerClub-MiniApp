import { create } from 'zustand';
import type { VkUser, AppTab, UserProfile, UpdateProfilePayload } from '../types';
import { useTournamentsStore } from './useTournamentsStore';
import { useRatingsStore } from './useRatingsStore';
import { usersApi } from '../api/usersApi';
import { triggerHaptic, initVkBridge } from '../utils/vkBridge';

interface UserState {
  vkUser: VkUser | null;
  profile: UserProfile | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  isLoading: boolean;
  isLoadingProfile: boolean;
  selectedCityId: number | null;
  selectedCity: number | null;
  selectedCityName: string;
  selectedClubId: number | null;
  activeTab: AppTab;
  isCityModalOpen: boolean;
  isLegalModalOpen: boolean;
  isProfileModalOpen: boolean;

  setUser: (user: VkUser | null) => void;
  setIsAdmin: (isAdmin: boolean) => void;
  setSelectedCity: (cityId: number | null, cityName: string) => void;
  setSelectedClub: (clubId: number | null) => void;
  setActiveTab: (tab: AppTab) => void;
  setIsCityModalOpen: (isOpen: boolean) => void;
  setIsLegalModalOpen: (isOpen: boolean) => void;
  setIsProfileModalOpen: (isOpen: boolean) => void;

  initUser: (user?: VkUser | null) => Promise<void>;
  getMe: () => Promise<UserProfile | null>;
  fetchProfile: () => Promise<UserProfile | null>;
  updateProfile: (data: UpdateProfilePayload) => Promise<UserProfile>;
  acceptTerms: () => Promise<void>;
  logout: () => void;
  resetUser: () => void;
}

export const useUserStore = create<UserState>((set, get) => ({
  vkUser: null,
  profile: null,
  isAuthenticated: false,
  isAdmin: false,
  isLoading: false,
  isLoadingProfile: false,
  selectedCityId: null,
  selectedCity: null,
  selectedCityName: 'Все города',
  selectedClubId: null,
  activeTab: 'schedule',
  isCityModalOpen: false,
  isLegalModalOpen: false,
  isProfileModalOpen: false,

  setUser: (user) => {
    const savedRole = typeof window !== 'undefined' ? localStorage.getItem('poker_is_admin') : null;
    const isAdmin = user?.isAdmin === true ? (savedRole !== null ? savedRole === 'true' : true) : false;
    if (typeof window !== 'undefined' && !user?.isAdmin) {
      localStorage.setItem('poker_is_admin', 'false');
    }
    set({
      vkUser: user,
      isAuthenticated: !!user,
      isAdmin,
      activeTab: isAdmin ? 'admin-tournaments' : 'schedule',
    });

    if (user) {
      get().fetchProfile();
    }
  },

  setIsAdmin: (isAdmin) => {
    const user = get().vkUser;
    const effectiveIsAdmin = user?.isAdmin === true ? isAdmin : false;
    if (typeof window !== 'undefined') {
      localStorage.setItem('poker_is_admin', effectiveIsAdmin ? 'true' : 'false');
    }
    set((state) => {
      let nextTab = state.activeTab;
      if (effectiveIsAdmin && state.activeTab === 'schedule') {
        nextTab = 'admin-tournaments';
      } else if (!effectiveIsAdmin && (state.activeTab === 'admin-tournaments' || state.activeTab === 'admin-create')) {
        nextTab = 'schedule';
      }
      return { isAdmin: effectiveIsAdmin, activeTab: nextTab };
    });

    // При переключении режима обновляем расписание в соответствии с ролью
    const { selectedCityId, selectedClubId } = get();
    if (effectiveIsAdmin) {
      useTournamentsStore.getState().fetchAdminSchedule(selectedCityId, selectedClubId);
    } else {
      useTournamentsStore.getState().fetchSchedule(selectedCityId, selectedClubId);
      useTournamentsStore.getState().fetchMyTournaments();
      useRatingsStore.getState().fetchLeaderboard();
    }
  },

  setSelectedCity: (cityId, cityName) => set({ selectedCityId: cityId, selectedCity: cityId, selectedCityName: cityName, selectedClubId: null }),
  setSelectedClub: (clubId) => set({ selectedClubId: clubId }),
  setActiveTab: (tab) => set({ activeTab: tab }),
  setIsCityModalOpen: (isOpen) => set({ isCityModalOpen: isOpen }),
  setIsLegalModalOpen: (isOpen) => set({ isLegalModalOpen: isOpen }),
  setIsProfileModalOpen: (isOpen) => set({ isProfileModalOpen: isOpen }),

  fetchProfile: async () => {
    set({ isLoading: true, isLoadingProfile: true });
    try {
      const profile = await usersApi.getMe();
      set({ profile });

      if (!get().vkUser && profile.vkId) {
        set({
          vkUser: {
            id: Number(profile.vkId) || 0,
            first_name: profile.firstName || profile.nickname || 'Игрок',
            last_name: profile.lastName || '',
            photo_200: profile.avatarUrl,
            photo_100: profile.avatarUrl,
            isAdmin: get().isAdmin,
          },
        });
      }

      const hasAcceptedTermsLocally = typeof window !== 'undefined' && localStorage.getItem('poker_legal_accepted') === 'true';
      const hasAcceptedTerms = !!profile.acceptedTermsAt || hasAcceptedTermsLocally;

      if (!hasAcceptedTerms) {
        set({ isLegalModalOpen: true });
      } else {
        const hasCompletedProfileLocally = typeof window !== 'undefined' && localStorage.getItem('poker_profile_completed') === 'true';
        const isProfileComplete = Boolean(profile.nickname && profile.phoneNumber) || hasCompletedProfileLocally;
        if (!isProfileComplete) {
          set({ isProfileModalOpen: true });
        }
      }

      return profile;
    } catch (error) {
      console.warn('Не удалось загрузить профиль пользователя:', error);
      // Если запрос профиля падает с 401 (например, некорректная подпись),
      // не блокируем рендер приложения — позволяем пользователю видеть расписание турниров как гостю.
      set({ isAuthenticated: true, activeTab: 'schedule' });
      return null;
    } finally {
      set({ isLoading: false, isLoadingProfile: false }); // Гарантированное снятие крутилки при 401 коде!
    }
  },

  initUser: async (user?: VkUser | null) => {
    set({ isLoading: true });
    try {
      if (user) {
        get().setUser(user);
      } else {
        const vkUser = await initVkBridge();
        if (vkUser) {
          get().setUser(vkUser);
        }
      }
    } catch (error) {
      console.warn('Не удалось инициализировать пользователя:', error);
      set({ isAuthenticated: true, activeTab: 'schedule' });
    } finally {
      set({ isLoading: false });
    }
  },

  getMe: async () => {
    return await get().fetchProfile();
  },

  updateProfile: async (data) => {
    const updated = await usersApi.updateProfile(data);
    const currentVkUser = get().vkUser;
    const vkUser: VkUser = currentVkUser || {
      id: Number(updated.vkId) || 0,
      first_name: updated.firstName || updated.nickname || 'Игрок',
      last_name: updated.lastName || '',
      photo_200: updated.avatarUrl,
      photo_100: updated.avatarUrl,
      isAdmin: get().isAdmin,
    };

    if (typeof window !== 'undefined') {
      localStorage.setItem('poker_profile_completed', 'true');
      if (updated.vkId) {
        localStorage.setItem('vk_test_user_id', updated.vkId);
      }
    }
    set({ profile: updated, vkUser, isProfileModalOpen: false, isAuthenticated: true });

    // Обновляем лидерборд и турниры, если изменились никнейм/рейтинг
    useRatingsStore.getState().fetchLeaderboard();
    useTournamentsStore.getState().fetchMyTournaments();

    return updated;
  },

  acceptTerms: async () => {
    const acceptedAt = new Date().toISOString();
    if (typeof window !== 'undefined') {
      localStorage.setItem('poker_legal_accepted', 'true');
      localStorage.setItem('poker_accepted_terms_at', acceptedAt);
    }

    if (!get().vkUser) {
      try {
        const user = await initVkBridge();
        if (user) {
          set({ vkUser: user });
        }
      } catch (err) {
        console.warn('Не удалось инициализировать пользователя при принятии условий:', err);
      }
    }

    try {
      await usersApi.acceptTerms();
    } catch (e) {
      console.warn('Не удалось зафиксировать согласие на бэкенде:', e);
    }

    set((state) => ({
      isLegalModalOpen: false,
      profile: state.profile ? { ...state.profile, acceptedTermsAt: acceptedAt } : state.profile,
    }));

    // Проверяем, нужно ли показать анкету после принятия оферты
    const { profile } = get();
    const hasCompletedProfileLocally = typeof window !== 'undefined' && localStorage.getItem('poker_profile_completed') === 'true';
    const isProfileComplete = Boolean(profile?.nickname && profile?.phoneNumber) || hasCompletedProfileLocally;
    if (!isProfileComplete) {
      set({ isProfileModalOpen: true });
    } else {
      set({ isAuthenticated: true });
    }
  },

  resetUser: () => {
    triggerHaptic('medium');

    if (typeof window !== 'undefined') {
      const keysToRemove = [
        'poker_legal_accepted',
        'poker_accepted_terms_at',
        'poker_profile_completed',
        'poker_is_admin',
        'vk_test_user_id',
        'tg_user_id',
      ];
      keysToRemove.forEach((key) => {
        try {
          localStorage.removeItem(key);
        } catch {
          // ignore
        }
      });
      try {
        for (let i = localStorage.length - 1; i >= 0; i--) {
          const key = localStorage.key(i);
          if (key && (key.startsWith('poker_') || key === 'vk_test_user_id' || key === 'tg_user_id')) {
            localStorage.removeItem(key);
          }
        }
      } catch {
        // ignore
      }
    }

    set({
      vkUser: null,
      profile: null,
      isAuthenticated: false,
      isAdmin: false,
      isLoading: false,
      isLoadingProfile: false,
      selectedCityId: null,
      selectedCity: null,
      selectedCityName: 'Все города',
      selectedClubId: null,
      activeTab: 'schedule',
      isCityModalOpen: false,
      isLegalModalOpen: true,
      isProfileModalOpen: false,
    });

    useTournamentsStore.setState({
      myTournaments: [],
      selectedTournament: null,
      isDetailModalOpen: false,
      actionError: null,
      scheduleError: null,
    });
  },

  logout: () => {
    get().resetUser();
  },
}));
