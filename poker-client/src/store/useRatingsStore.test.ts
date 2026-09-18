import { describe, it, expect, beforeEach, mock } from 'bun:test';
import { useRatingsStore } from './useRatingsStore';
import { ratingsApi } from '../api/ratingsApi';

describe('useRatingsStore pagination', () => {
  beforeEach(() => {
    useRatingsStore.setState({
      leaderboard: [],
      totalCount: 0,
      isLoading: false,
      isLoadingMore: false,
      seasonTab: 'current',
    });
  });

  it('fetchLeaderboard sets initial 50 items and totalCount', async () => {
    const mockItems = Array.from({ length: 50 }, (_, i) => ({
      rank: i + 1,
      id: i + 1,
      vkId: `vk_${i + 1}`,
      firstName: `Player${i + 1}`,
      totalRating: 1000 - i,
      seasonRating: 1000 - i,
      points: 1000 - i,
    }));

    const originalGetLeaderboard = ratingsApi.getLeaderboard;
    ratingsApi.getLeaderboard = mock(async () => ({
      items: mockItems,
      totalCount: 120,
      limit: 50,
      offset: 0,
    }));

    try {
      await useRatingsStore.getState().fetchLeaderboard('season');

      const state = useRatingsStore.getState();
      expect(state.leaderboard.length).toBe(50);
      expect(state.totalCount).toBe(120);
      expect(state.isLoading).toBe(false);
      expect(state.leaderboard[0].rank).toBe(1);
    } finally {
      ratingsApi.getLeaderboard = originalGetLeaderboard;
    }
  });

  it('loadMore appends next page and prevents duplicates', async () => {
    const page1Items = Array.from({ length: 50 }, (_, i) => ({
      rank: i + 1,
      id: i + 1,
      vkId: `vk_${i + 1}`,
      firstName: `Player${i + 1}`,
      totalRating: 1000 - i,
      seasonRating: 1000 - i,
      points: 1000 - i,
    }));

    const page2Items = Array.from({ length: 20 }, (_, i) => ({
      rank: 51 + i,
      id: 51 + i,
      vkId: `vk_${51 + i}`,
      firstName: `Player${51 + i}`,
      totalRating: 950 - i,
      seasonRating: 950 - i,
      points: 950 - i,
    }));

    useRatingsStore.setState({
      leaderboard: page1Items,
      totalCount: 70,
    });

    const originalGetLeaderboard = ratingsApi.getLeaderboard;
    ratingsApi.getLeaderboard = mock(async () => ({
      items: page2Items,
      totalCount: 70,
      limit: 50,
      offset: 50,
    }));

    try {
      await useRatingsStore.getState().loadMore();

      const state = useRatingsStore.getState();
      expect(state.leaderboard.length).toBe(70);
      expect(state.leaderboard[50].rank).toBe(51);
      expect(state.leaderboard[69].rank).toBe(70);
      expect(state.isLoadingMore).toBe(false);
    } finally {
      ratingsApi.getLeaderboard = originalGetLeaderboard;
    }
  });

  it('loadMore does nothing if all items already loaded', async () => {
    const items = Array.from({ length: 50 }, (_, i) => ({
      rank: i + 1,
      id: i + 1,
      vkId: `vk_${i + 1}`,
      firstName: `Player${i + 1}`,
      totalRating: 1000 - i,
    }));

    useRatingsStore.setState({
      leaderboard: items,
      totalCount: 50,
    });

    const originalGetLeaderboard = ratingsApi.getLeaderboard;
    const mockFn = mock(async () => ({
      items: [],
      totalCount: 50,
      limit: 50,
      offset: 50,
    }));
    ratingsApi.getLeaderboard = mockFn;

    try {
      await useRatingsStore.getState().loadMore();
      expect(mockFn).not.toHaveBeenCalled();
      expect(useRatingsStore.getState().leaderboard.length).toBe(50);
    } finally {
      ratingsApi.getLeaderboard = originalGetLeaderboard;
    }
  });

  it('loadMore prevents duplicate concurrent requests on rapid double click', async () => {
    const page1Items = Array.from({ length: 50 }, (_, i) => ({
      rank: i + 1,
      id: i + 1,
      vkId: `vk_${i + 1}`,
      firstName: `Player${i + 1}`,
      totalRating: 1000 - i,
      seasonRating: 1000 - i,
      points: 1000 - i,
    }));

    useRatingsStore.setState({
      leaderboard: page1Items,
      totalCount: 100,
    });

    let calls = 0;
    const originalGetLeaderboard = ratingsApi.getLeaderboard;
    ratingsApi.getLeaderboard = mock(async () => {
      calls++;
      await new Promise((resolve) => setTimeout(resolve, 50));
      return {
        items: [{ rank: 51, id: 51, vkId: 'vk_51', totalRating: 500 }],
        totalCount: 100,
        limit: 50,
        offset: 50,
      };
    });

    try {
      const p1 = useRatingsStore.getState().loadMore();
      const p2 = useRatingsStore.getState().loadMore(); // concurrent call
      await Promise.all([p1, p2]);

      expect(calls).toBe(1);
    } finally {
      ratingsApi.getLeaderboard = originalGetLeaderboard;
    }
  });

  it('loadMore discards response if active tab was changed during fetch', async () => {
    const page1Items = Array.from({ length: 50 }, (_, i) => ({
      rank: i + 1,
      id: i + 1,
      vkId: `vk_${i + 1}`,
      firstName: `Player${i + 1}`,
      totalRating: 1000 - i,
      seasonRating: 1000 - i,
      points: 1000 - i,
    }));

    useRatingsStore.setState({
      leaderboard: page1Items,
      totalCount: 100,
      seasonTab: 'current',
    });

    const originalGetLeaderboard = ratingsApi.getLeaderboard;
    ratingsApi.getLeaderboard = mock(async () => {
      await new Promise((resolve) => setTimeout(resolve, 30));
      return {
        items: [{ rank: 51, id: 51, vkId: 'vk_51', totalRating: 500 }],
        totalCount: 100,
        limit: 50,
        offset: 50,
      };
    });

    try {
      const loadMorePromise = useRatingsStore.getState().loadMore();
      // User switches tab while loadMore is in flight
      useRatingsStore.getState().setSeasonTab('all-time');
      await loadMorePromise;

      // The stale items should NOT be appended to the new tab
      expect(useRatingsStore.getState().leaderboard.length).toBe(50);
    } finally {
      ratingsApi.getLeaderboard = originalGetLeaderboard;
    }
  });
});
