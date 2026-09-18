import { create } from 'zustand';
import type { LeaderboardEntry } from '../types';
import { ratingsApi } from '../api/ratingsApi';

interface RatingsState {
  leaderboard: LeaderboardEntry[];
  totalCount: number;
  isLoading: boolean;
  isLoadingMore: boolean;
  seasonTab: 'current' | 'all-time';

  fetchLeaderboard: (typeOrLimit?: 'season' | 'all' | number, limit?: number) => Promise<void>;
  loadMore: () => Promise<void>;
  setSeasonTab: (tab: 'current' | 'all-time') => void;
}

export const useRatingsStore = create<RatingsState>((set, get) => ({
  leaderboard: [],
  totalCount: 0,
  isLoading: false,
  isLoadingMore: false,
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

    const requestedTab = activeType === 'all' ? 'all-time' : 'current';

    set({ isLoading: true });
    try {
      const data = await ratingsApi.getLeaderboard(activeType, activeLimit, 0);
      // Guard against race conditions: ignore stale responses if active tab switched
      if (get().seasonTab !== requestedTab) {
        return;
      }
      set({
        leaderboard: data.items,
        totalCount: data.totalCount,
      });
    } catch (err) {
      console.error('Ошибка загрузки рейтинга:', err);
    } finally {
      if (get().seasonTab === requestedTab) {
        set({ isLoading: false });
      }
    }
  },

  loadMore: async () => {
    const { leaderboard, totalCount, isLoadingMore, isLoading, seasonTab } = get();
    if (isLoadingMore || isLoading) return;
    if (totalCount > 0 && leaderboard.length >= totalCount) return;

    const requestedTab = seasonTab;
    set({ isLoadingMore: true });
    try {
      const activeType: 'season' | 'all' = requestedTab === 'all-time' ? 'all' : 'season';
      const offset = leaderboard.length;
      const data = await ratingsApi.getLeaderboard(activeType, 50, offset);

      // Guard against race conditions: ignore stale pages if tab switched
      if (get().seasonTab !== requestedTab) {
        return;
      }

      const existingIds = new Set(get().leaderboard.map((u) => u.id));
      const newItems = data.items.filter((u) => !existingIds.has(u.id));

      set({
        leaderboard: [...get().leaderboard, ...newItems],
        totalCount: data.totalCount,
      });
    } catch (err) {
      console.error('Ошибка подгрузки рейтинга:', err);
    } finally {
      set({ isLoadingMore: false });
    }
  },

  setSeasonTab: (tab) => set({ seasonTab: tab, isLoadingMore: false }),
}));

