import { apiClient } from './apiClient';
import type { LeaderboardEntry, LeaderboardResponse } from '../types';

export const ratingsApi = {
  async getLeaderboard(
    typeOrLimit: 'season' | 'all' | number = 'season',
    limitOrType: number | 'season' | 'all' = 50,
    offset = 0
  ): Promise<LeaderboardResponse> {
    let type: 'season' | 'all' = 'season';
    let limit = 50;

    if (typeof typeOrLimit === 'number') {
      limit = typeOrLimit;
      if (typeof limitOrType === 'string') {
        type = limitOrType;
      }
    } else {
      type = typeOrLimit;
      if (typeof limitOrType === 'number') {
        limit = limitOrType;
      }
    }

    const response = await apiClient.get<LeaderboardResponse | LeaderboardEntry[]>(
      `/api/ratings/leaderboard?type=${type}&limit=${limit}&offset=${offset}`
    );
    const data = response.data;
    if (Array.isArray(data)) {
      return {
        items: data,
        totalCount: data.length,
        limit,
        offset,
      };
    }
    return data;
  },

  async assignPoints(tournamentId: number, userPoints: Record<number, number>): Promise<{ message: string }> {
    const response = await apiClient.post<{ message: string }>('/api/ratings/admin/assign-points', {
      tournamentId,
      userPoints,
    });
    return response.data;
  },

  async syncSheets(): Promise<{ success: boolean; message: string; totalProcessed: number; updatedCount: number; createdCount: number }> {
    const response = await apiClient.post<{ success: boolean; message: string; totalProcessed: number; updatedCount: number; createdCount: number }>('/api/admin/sync-sheets');
    return response.data;
  },
};

