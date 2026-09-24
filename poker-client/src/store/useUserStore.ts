import { create } from 'zustand';
import type { VkUser, AppTab, UserProfile, UpdateProfilePayload } from '../types';
import { useTournamentsStore } from './useTournamentsStore';
import { useRatingsStore } from './useRatingsStore';
import { usersApi } from '../api/usersApi';
import { triggerHaptic, initVkBridge } from '../utils/vkBridge';
import { isMasterClubCard } from '../constants/auth';

interface UserState {
  vkUser: VkUser | null;
  profile: UserProfile | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  hasAdminRole: boolean;
  activeRole: 'admin' | 'player';
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
  setIsLoading: (isLoading: boolean) => void;
  setIsAdmin: (isAdmin: boolean) => void;
  setActiveRole: (role: 'admin' | 'player') => void;
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

const getInitialState = () => {
  const hasAdmin = typeof window !== 'undefined' && localStorage.getItem('poker_has_admin_role') === 'true';
  const savedActiveRole = typeof window !== 'undefined' ? localStorage.getItem('poker_active_role') : null;
  const savedRole = typeof window !== 'undefined' ? localStorage.getItem('poker_is_admin') : null;
  const activeRole: 'admin' | 'player' = hasAdmin ? (savedActiveRole === 'player' || savedRole === 'false' ? 'player' : 'admin') : 'player';
  const isAdmin = hasAdmin && activeRole === 'admin';

  let selectedCityId: number | null = 1;
  let selectedCityName = 'Пермь';
  if (typeof window !== 'undefined') {
    const savedCityId = localStorage.getItem('poker_selected_city_id');
    const savedCityName = localStorage.getItem('poker_selected_city_name');
    if (savedCityId === 'all' || savedCityId === '0') {
      selectedCityId = null;
      selectedCityName = 'Все города';
    } else if (savedCityId) {
      const parsed = Number(savedCityId);
      if (!isNaN(parsed) && parsed > 0) {
        selectedCityId = parsed;
        selectedCityName = savedCityName || 'Пермь';
      }
    } else {
      selectedCityId = 1;
      selectedCityName = savedCityName || 'Пермь';
    }
  }

  return {
    hasAdminRole: hasAdmin,
    activeRole,
    isAdmin,
    activeTab: (isAdmin ? 'admin-tournaments' : 'schedule') as AppTab,
    selectedCityId,
    selectedCityName,
  };
};

const initialState = getInitialState();

export const useUserStore = create<UserState>((set, get) => ({
  vkUser: null,
  profile: null,
  isAuthenticated: false,
  isAdmin: initialState.isAdmin,
  hasAdminRole: initialState.hasAdminRole,
  activeRole: initialState.activeRole,
  isLoading: false,
  isLoadingProfile: false,
  selectedCityId: initialState.selectedCityId,
  selectedCity: initialState.selectedCityId,
  selectedCityName: initialState.selectedCityName,
  selectedClubId: null,
  activeTab: initialState.activeTab,
  isCityModalOpen: false,
  isLegalModalOpen: false,
  isProfileModalOpen: false,

  setIsLoading: (isLoading) => {
    set({ isLoading });
    if (isLoading) {
      setTimeout(() => {
        if (get().isLoading) {
          set({ isLoading: false, isLoadingProfile: false });
        }
      }, 2000);
    }
  },

  setUser: (user) => {
    const hasAdminRight = user?.isAdmin === true || get().hasAdminRole || (typeof window !== 'undefined' && localStorage.getItem('poker_has_admin_role') === 'true');
    const savedActiveRole = typeof window !== 'undefined' ? localStorage.getItem('poker_active_role') : null;
    const savedRole = typeof window !== 'undefined' ? localStorage.getItem('poker_is_admin') : null;

    let activeRole: 'admin' | 'player' = 'player';
    if (hasAdminRight) {
      if (savedActiveRole === 'player' || savedRole === 'false') {
        activeRole = 'player';
      } else {
        activeRole = 'admin';
      }
    }

    const isAdmin = hasAdminRight && activeRole === 'admin';

    if (typeof window !== 'undefined') {
      if (user?.photo_200) {
        localStorage.setItem('vk_avatar_url', user.photo_200);
      }
      if (hasAdminRight) {
        localStorage.setItem('poker_has_admin_role', 'true');
        localStorage.setItem('poker_is_admin', isAdmin ? 'true' : 'false');
        localStorage.setItem('poker_active_role', activeRole);
      } else {
        localStorage.setItem('poker_is_admin', 'false');
        if (!localStorage.getItem('poker_has_admin_role')) {
          localStorage.setItem('poker_active_role', 'player');
        }
      }
    }

    set({
      vkUser: user ? { ...user, isAdmin: hasAdminRight } : null,
      hasAdminRole: hasAdminRight,
      activeRole,
      isAuthenticated: !!user,
      isAdmin,
      activeTab: isAdmin ? 'admin-tournaments' : 'schedule',
    });

    if (user) {
      get().fetchProfile();
    }
  },

  setIsAdmin: (isAdmin) => {
    const { vkUser, profile, hasAdminRole } = get();
    const canBeAdmin = hasAdminRole || vkUser?.isAdmin === true || profile?.isAdmin === true;
    const effectiveIsAdmin = canBeAdmin ? isAdmin : false;
    const activeRole: 'admin' | 'player' = effectiveIsAdmin ? 'admin' : 'player';

    if (typeof window !== 'undefined') {
      if (canBeAdmin) {
        localStorage.setItem('poker_has_admin_role', 'true');
        localStorage.setItem('poker_is_admin', effectiveIsAdmin ? 'true' : 'false');
        localStorage.setItem('poker_active_role', activeRole);
      } else {
        localStorage.removeItem('poker_has_admin_role');
        localStorage.setItem('poker_is_admin', 'false');
        localStorage.setItem('poker_active_role', 'player');
      }
    }

    set((state) => {
      let nextTab = state.activeTab;
      if (effectiveIsAdmin && (state.activeTab === 'schedule' || state.activeTab === 'profile')) {
        nextTab = 'admin-tournaments';
      } else if (!effectiveIsAdmin && (state.activeTab === 'admin-tournaments' || state.activeTab === 'admin-create')) {
        nextTab = 'schedule';
      }
      return { 
        isAdmin: effectiveIsAdmin, 
        activeRole,
        activeTab: nextTab 
      };
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

  setActiveRole: (role) => {
    get().setIsAdmin(role === 'admin');
  },

  setSelectedCity: (cityId, cityName) => {
    if (typeof window !== 'undefined') {
      if (cityId !== null && cityId !== undefined && Number(cityId) > 0) {
        localStorage.setItem('poker_selected_city_id', cityId.toString());
        localStorage.setItem('poker_selected_city_name', cityName);
      } else {
        localStorage.setItem('poker_selected_city_id', 'all');
        localStorage.setItem('poker_selected_city_name', 'Все города');
      }
    }
    set({ selectedCityId: cityId, selectedCity: cityId, selectedCityName: cityName, selectedClubId: null });
  },
  setSelectedClub: (clubId) => set({ selectedClubId: clubId }),
  setActiveTab: (tab) => set({ activeTab: tab }),
  setIsCityModalOpen: (isOpen) => set({ isCityModalOpen: isOpen }),
  setIsLegalModalOpen: (isOpen) => set({ isLegalModalOpen: isOpen }),
  setIsProfileModalOpen: (isOpen) => set({ isProfileModalOpen: isOpen }),

  fetchProfile: async () => {
    set({ isLoading: true, isLoadingProfile: true });

    // Жесткий аварийный таймаут на 2 секунды: защита от вечного спиннера на медленном 4G/LTE
    const emergencyTimer = setTimeout(() => {
      if (get().isLoading || get().isLoadingProfile) {
        console.warn('Аварийный таймаут 2с в fetchProfile сработал: принудительно снимаем крутилку');
        set({ isLoading: false, isLoadingProfile: false, isAuthenticated: true, activeTab: 'schedule' });
      }
    }, 2000);

    try {
      const vkUserAvatar = get().vkUser?.photo_200 || get().vkUser?.photo_100;
      const profile = await usersApi.getMe(vkUserAvatar);
      const isMaster = isMasterClubCard(profile?.clubCardId);
      const isProfileAdmin = profile?.isAdmin === true || isMaster;
      const currentVkUser = get().vkUser;
      const hasAdminRole = isProfileAdmin;

      const savedActiveRole = typeof window !== 'undefined' ? localStorage.getItem('poker_active_role') : null;
      const savedIsAdmin = typeof window !== 'undefined' ? localStorage.getItem('poker_is_admin') : null;

      let activeRole: 'admin' | 'player' = 'player';
      if (hasAdminRole) {
        if (savedActiveRole === 'player' || savedIsAdmin === 'false') {
          activeRole = 'player';
        } else {
          activeRole = 'admin';
        }
      } else {
        activeRole = 'player';
      }

      const effectiveIsAdmin = hasAdminRole && activeRole === 'admin';

      if (typeof window !== 'undefined') {
        if (hasAdminRole) {
          localStorage.setItem('poker_has_admin_role', 'true');
          localStorage.setItem('poker_active_role', activeRole);
          localStorage.setItem('poker_is_admin', effectiveIsAdmin ? 'true' : 'false');
        } else {
          localStorage.removeItem('poker_has_admin_role');
          localStorage.setItem('poker_active_role', 'player');
          localStorage.setItem('poker_is_admin', 'false');
        }
      }

      const updatedVkUser: VkUser = currentVkUser
        ? { ...currentVkUser, isAdmin: hasAdminRole }
        : {
            id: Number(profile.vkId) || 0,
            first_name: profile.firstName || profile.nickname || 'Игрок',
            last_name: profile.lastName || '',
            photo_200: profile.avatarUrl,
            photo_100: profile.avatarUrl,
            isAdmin: hasAdminRole,
          };

      set((state) => ({
        profile,
        vkUser: updatedVkUser,
        hasAdminRole,
        activeRole,
        isAdmin: effectiveIsAdmin,
        activeTab: effectiveIsAdmin
          ? ((state.activeTab === 'schedule' || state.activeTab === 'profile') ? 'admin-tournaments' : state.activeTab)
          : (state.activeTab === 'admin-tournaments' || state.activeTab === 'admin-create' ? 'schedule' : state.activeTab),
      }));

      const hasAcceptedTermsLocally = typeof window !== 'undefined' && localStorage.getItem('poker_legal_accepted') === 'true';
      const hasAcceptedTerms = !!profile.acceptedTermsAt || hasAcceptedTermsLocally;

      if (!hasAcceptedTerms) {
        set({ isLegalModalOpen: true });
      } else {
        const hasClubCard = Boolean(profile.clubCardId && profile.clubCardId.trim().length > 0);
        const hasCompletedProfileLocally = typeof window !== 'undefined' && localStorage.getItem('poker_profile_completed') === 'true';
        const isProfileComplete = hasClubCard && (Boolean(profile.nickname && profile.phoneNumber && (profile.fullName || profile.firstName)) || hasCompletedProfileLocally);
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
      clearTimeout(emergencyTimer);
      set({ isLoading: false, isLoadingProfile: false }); // Гарантированное снятие крутилки при 401 коде!
    }
  },

  initUser: async (user?: VkUser | null) => {
    set({ isLoading: true });

    // Жесткий аварийный таймаут на 2 секунды: защита от вечного спиннера
    const emergencyTimer = setTimeout(() => {
      if (get().isLoading) {
        console.warn('Аварийный таймаут 2с в initUser сработал: принудительно снимаем крутилку');
        set({ isLoading: false, isAuthenticated: true, activeTab: 'schedule' });
      }
    }, 2000);

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
      clearTimeout(emergencyTimer);
      set({ isLoading: false });
    }
  },

  getMe: async () => {
    return await get().fetchProfile();
  },

  updateProfile: async (data) => {
    const updated = await usersApi.updateProfile(data);
    const currentVkUser = get().vkUser;
    const isMaster = isMasterClubCard(updated.clubCardId);
    const hasAdmin = get().hasAdminRole || currentVkUser?.isAdmin === true || updated.isAdmin === true || isMaster;
    const vkUser: VkUser = currentVkUser ? {
      ...currentVkUser,
      isAdmin: hasAdmin,
    } : {
      id: Number(updated.vkId) || 0,
      first_name: updated.firstName || updated.nickname || 'Игрок',
      last_name: updated.lastName || '',
      photo_200: updated.avatarUrl,
      photo_100: updated.avatarUrl,
      isAdmin: hasAdmin,
    };

    if (typeof window !== 'undefined') {
      localStorage.setItem('poker_profile_completed', 'true');
      if (updated.vkId) {
        localStorage.setItem('vk_test_user_id', updated.vkId);
      }
      if (hasAdmin) {
        localStorage.setItem('poker_has_admin_role', 'true');
        if (isMaster) {
          localStorage.setItem('poker_active_role', 'admin');
          localStorage.setItem('poker_is_admin', 'true');
        }
      }
    }

    const effectiveIsAdmin = isMaster ? true : (hasAdmin && get().activeRole === 'admin');
    const activeRole = isMaster ? 'admin' : get().activeRole;

    set((state) => ({
      profile: { ...updated, isAdmin: hasAdmin },
      vkUser,
      hasAdminRole: hasAdmin,
      activeRole,
      isAdmin: effectiveIsAdmin,
      activeTab: isMaster
        ? 'admin-tournaments'
        : (effectiveIsAdmin && (state.activeTab === 'schedule' || state.activeTab === 'profile') ? 'admin-tournaments' : state.activeTab),
      isProfileModalOpen: false,
      isAuthenticated: true
    }));

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
    const hasClubCard = Boolean(profile?.clubCardId && profile.clubCardId.trim().length > 0);
    const hasCompletedProfileLocally = typeof window !== 'undefined' && localStorage.getItem('poker_profile_completed') === 'true';
    const isProfileComplete = hasClubCard && (Boolean(profile?.nickname && profile?.phoneNumber && (profile?.fullName || profile?.firstName)) || hasCompletedProfileLocally);
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
        'poker_has_admin_role',
        'poker_is_admin',
        'poker_active_role',
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
      hasAdminRole: false,
      activeRole: 'player',
      isLoading: false,
      isLoadingProfile: false,
      selectedCityId: 1,
      selectedCity: 1,
      selectedCityName: 'Пермь',
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
