import { create } from 'zustand';
import type { LeaderboardEntry } from '../types';
import { ratingsApi } from '../api/ratingsApi';

interface RatingsState {
  leaderboard: LeaderboardEntry[];
  totalCount: number;
  isLoading: boolean;
  isLoadingMore: boolean;
  seasonTab: 'current' | 'all-time' | 'all';
  seasonName: string;

  fetchLeaderboard: (typeOrLimit?: 'season' | 'all' | number | boolean, limit?: number, showLoader?: boolean) => Promise<void>;
  loadMore: () => Promise<void>;
  setSeasonTab: (tab: 'current' | 'all-time' | 'all') => void;
  setSeasonName: (name: string) => void;
}

export const useRatingsStore = create<RatingsState>((set, get) => ({
  leaderboard: [],
  totalCount: 0,
  isLoading: false,
  isLoadingMore: false,
  seasonTab: 'current',
  seasonName: 'Осень 2026',

  fetchLeaderboard: async (typeOrLimit?: 'season' | 'all' | number | boolean, limit = 50, showLoader = true) => {
    let activeType: 'season' | 'all' = (get().seasonTab === 'all-time' || get().seasonTab === 'all') ? 'all' : 'season';
    let activeLimit = limit;
    let shouldShowLoader = showLoader;

    if (typeof typeOrLimit === 'boolean') {
      shouldShowLoader = typeOrLimit;
    } else if (typeof typeOrLimit === 'number') {
      activeLimit = typeOrLimit;
    } else if (typeOrLimit === 'season' || typeOrLimit === 'all') {
      activeType = typeOrLimit;
      set({ seasonTab: typeOrLimit === 'all' ? 'all' : 'current' });
    }

    const requestedTab = activeType === 'all' ? 'all' : 'current';

    if (shouldShowLoader) {
      set({ isLoading: true });
    }
    try {
      const fetchLimit = Math.max(activeLimit, get().leaderboard.length || 50);
      const data = await ratingsApi.getLeaderboard(activeType, fetchLimit, 0);
      // Guard against race conditions: ignore stale responses if active tab switched
      const currentIsAll = get().seasonTab === 'all' || get().seasonTab === 'all-time';
      const requestedIsAll = requestedTab === 'all';
      if (currentIsAll !== requestedIsAll) {
        return;
      }
      set({
        leaderboard: data.items,
        totalCount: data.totalCount,
        seasonName: data.seasonName?.trim() || get().seasonName || 'Осень 2026',
      });
    } catch (err) {
      console.error('Ошибка загрузки рейтинга:', err);
    } finally {
      const currentIsAll = get().seasonTab === 'all' || get().seasonTab === 'all-time';
      const requestedIsAll = requestedTab === 'all';
      if (shouldShowLoader && currentIsAll === requestedIsAll) {
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
      const isAll = requestedTab === 'all' || requestedTab === 'all-time';
      const activeType: 'season' | 'all' = isAll ? 'all' : 'season';
      const offset = leaderboard.length;
      const data = await ratingsApi.getLeaderboard(activeType, 50, offset);

      // Guard against race conditions: ignore stale pages if tab switched
      const currentIsAll = get().seasonTab === 'all' || get().seasonTab === 'all-time';
      if (currentIsAll !== isAll) {
        return;
      }

      const existingIds = new Set(get().leaderboard.map((u) => u.id));
      const newItems = data.items.filter((u) => !existingIds.has(u.id));

      set({
        leaderboard: [...get().leaderboard, ...newItems],
        totalCount: data.totalCount,
        seasonName: data.seasonName?.trim() || get().seasonName || 'Осень 2026',
      });
    } catch (err) {
      console.error('Ошибка подгрузки рейтинга:', err);
    } finally {
      set({ isLoadingMore: false });
    }
  },

  setSeasonTab: (tab) => set({ seasonTab: tab, isLoadingMore: false }),
  setSeasonName: (name) => set({ seasonName: name?.trim() || 'Осень 2026' }),
}));

