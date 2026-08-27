import { create } from 'zustand';
import type { LeaderboardEntry } from '../types';
import { ratingsApi } from '../api/ratingsApi';

interface RatingsState {
  leaderboard: LeaderboardEntry[];
  isLoading: boolean;
  seasonTab: 'current' | 'all-time';

  fetchLeaderboard: (limit?: number) => Promise<void>;
  setSeasonTab: (tab: 'current' | 'all-time') => void;
}

export const useRatingsStore = create<RatingsState>((set) => ({
  leaderboard: [],
  isLoading: false,
  seasonTab: 'current',

  fetchLeaderboard: async (limit = 50) => {
    set({ isLoading: true });
    try {
      const data = await ratingsApi.getLeaderboard(limit);
      set({ leaderboard: data });
    } catch (err) {
      console.error('Ошибка загрузки рейтинга:', err);
    } finally {
      set({ isLoading: false });
    }
  },

  setSeasonTab: (tab) => set({ seasonTab: tab }),
}));
