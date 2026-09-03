import { apiClient } from './apiClient';
import type { Tournament, TournamentDetail, CreateTournamentRequest } from '../types';

export const tournamentsApi = {
  async getSchedule(cityId?: number, clubId?: number, includeFinished?: boolean): Promise<Tournament[]> {
    const params = new URLSearchParams();
    if (cityId) params.append('cityId', cityId.toString());
    if (clubId) params.append('clubId', clubId.toString());
    if (includeFinished) params.append('includeFinished', 'true');

    const response = await apiClient.get<Tournament[]>(`/api/tournaments/schedule?${params.toString()}`);
    return response.data;
  },

  async getTournament(id: number): Promise<TournamentDetail> {
    const response = await apiClient.get<TournamentDetail>(`/api/tournaments/${id}`);
    return response.data;
  },

  async getMyTournaments(): Promise<Tournament[]> {
    const response = await apiClient.get<Tournament[]>('/api/tournaments/my');
    return response.data;
  },

  async register(
    tournamentId: number, 
    profile?: { vkId?: string; firstName?: string; lastName?: string; avatarUrl?: string }
  ): Promise<{ message: string }> {
    const response = await apiClient.post<{ message: string }>('/api/tournaments/register', {
      tournamentId,
      ...profile,
    });
    return response.data;
  },

  async unregister(tournamentId: number, vkId?: string): Promise<{ message: string }> {
    const response = await apiClient.post<{ message: string }>('/api/tournaments/unregister', {
      tournamentId,
      vkId,
    });
    return response.data;
  },

  async createTournament(data: CreateTournamentRequest): Promise<Tournament> {
    const response = await apiClient.post<Tournament>('/api/tournaments', data);
    return response.data;
  },
};
