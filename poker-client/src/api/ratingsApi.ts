import { apiClient } from './apiClient';
import type { LeaderboardEntry } from '../types';

export const ratingsApi = {
  async getLeaderboard(limit = 50): Promise<LeaderboardEntry[]> {
    const response = await apiClient.get<LeaderboardEntry[]>(`/api/ratings/leaderboard?limit=${limit}`);
    return response.data;
  },

  async assignPoints(tournamentId: number, userPoints: Record<number, number>): Promise<{ message: string }> {
    const response = await apiClient.post<{ message: string }>('/api/ratings/admin/assign-points', {
      tournamentId,
      userPoints,
    });
    return response.data;
  },
};
