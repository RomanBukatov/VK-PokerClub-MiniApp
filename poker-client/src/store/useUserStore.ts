import { create } from 'zustand';
import type { VkUser, AppTab, UserProfile, UpdateProfilePayload } from '../types';
import { useTournamentsStore } from './useTournamentsStore';
import { useRatingsStore } from './useRatingsStore';
import { usersApi } from '../api/usersApi';

interface UserState {
  vkUser: VkUser | null;
  profile: UserProfile | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
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

  fetchProfile: () => Promise<UserProfile | null>;
  updateProfile: (data: UpdateProfilePayload) => Promise<UserProfile>;
  acceptTerms: () => Promise<void>;
}

export const useUserStore = create<UserState>((set, get) => ({
  vkUser: null,
  profile: null,
  isAuthenticated: false,
  isAdmin: false,
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
    const isAdmin = savedRole !== null ? savedRole === 'true' : (user?.isAdmin ?? true);
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
    if (typeof window !== 'undefined') {
      localStorage.setItem('poker_is_admin', isAdmin ? 'true' : 'false');
    }
    set((state) => {
      let nextTab = state.activeTab;
      if (isAdmin && state.activeTab === 'schedule') {
        nextTab = 'admin-tournaments';
      } else if (!isAdmin && (state.activeTab === 'admin-tournaments' || state.activeTab === 'admin-create')) {
        nextTab = 'schedule';
      }
      return { isAdmin, activeTab: nextTab };
    });

    // При переключении режима обновляем расписание в соответствии с ролью
    const { selectedCityId, selectedClubId } = get();
    if (isAdmin) {
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
    set({ isLoadingProfile: true });
    try {
      const profile = await usersApi.getMe();
      set({ profile, isLoadingProfile: false });

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
      set({ isLoadingProfile: false });
      return null;
    }
  },

  updateProfile: async (data) => {
    const updated = await usersApi.updateProfile(data);
    if (typeof window !== 'undefined') {
      localStorage.setItem('poker_profile_completed', 'true');
    }
    set({ profile: updated, isProfileModalOpen: false });

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
    }
  },
}));
