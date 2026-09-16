import { create } from 'zustand';
import type { LeaderboardEntry } from '../types';
import { ratingsApi } from '../api/ratingsApi';

interface RatingsState {
  leaderboard: LeaderboardEntry[];
  isLoading: boolean;
  seasonTab: 'current' | 'all-time';

  fetchLeaderboard: (type?: 'season' | 'all' | number, limit?: number) => Promise<void>;
  setSeasonTab: (tab: 'current' | 'all-time') => void;
}

export const useRatingsStore = create<RatingsState>((set, get) => ({
  leaderboard: [],
  isLoading: false,
  seasonTab: 'current',

  fetchLeaderboard: async (typeOrLimit?: 'season' | 'all' | number, limit = 50) => {
    let activeType: 'season' | 'all' = get().seasonTab === 'all-time' ? 'all' : 'season';
    let activeLimit = limit;

    if (typeof typeOrLimit === 'number') {
      activeLimit = typeOrLimit;
    } else if (typeOrLimit === 'season' || typeOrLimit === 'all') {
      activeType = typeOrLimit;
      set({ seasonTab: typeOrLimit === 'all' ? 'all-time' : 'current' });
    }

    set({ isLoading: true });
    try {
      const data = await ratingsApi.getLeaderboard(activeType, activeLimit);
      set({ leaderboard: data });
    } catch (err) {
      console.error('Ошибка загрузки рейтинга:', err);
    } finally {
      set({ isLoading: false });
    }
  },

  setSeasonTab: (tab) => set({ seasonTab: tab }),
}));
