import { create } from 'zustand';
import type { VkUser, AppTab } from '../types';

interface UserState {
  vkUser: VkUser | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  selectedCityId: number | null;
  selectedCityName: string;
  selectedClubId: number | null;
  activeTab: AppTab;
  isCityModalOpen: boolean;

  setUser: (user: VkUser | null) => void;
  setIsAdmin: (isAdmin: boolean) => void;
  setSelectedCity: (cityId: number | null, cityName: string) => void;
  setSelectedClub: (clubId: number | null) => void;
  setActiveTab: (tab: AppTab) => void;
  setIsCityModalOpen: (isOpen: boolean) => void;
}

export const useUserStore = create<UserState>((set) => ({
  vkUser: null,
  isAuthenticated: false,
  isAdmin: false,
  selectedCityId: null,
  selectedCityName: 'Все города',
  selectedClubId: null,
  activeTab: 'schedule',
  isCityModalOpen: false,

  setUser: (user) => set({ 
    vkUser: user, 
    isAuthenticated: !!user,
    isAdmin: user?.isAdmin ?? false
  }),
  setIsAdmin: (isAdmin) => set({ isAdmin }),
  setSelectedCity: (cityId, cityName) => set({ selectedCityId: cityId, selectedCityName: cityName, selectedClubId: null }),
  setSelectedClub: (clubId) => set({ selectedClubId: clubId }),
  setActiveTab: (tab) => set({ activeTab: tab }),
  setIsCityModalOpen: (isOpen) => set({ isCityModalOpen: isOpen }),
}));
