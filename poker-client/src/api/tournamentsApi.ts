import { apiClient } from './apiClient';
import type { Tournament, TournamentDetail, CreateTournamentRequest, UpdateTournamentRequest } from '../types';

export const tournamentsApi = {
  async getSchedule(cityId?: number | null, clubId?: number | null, includeFinished?: boolean): Promise<Tournament[]> {
    const params = new URLSearchParams();
    if (cityId !== null && cityId !== undefined && Number(cityId) > 0) {
      params.append('cityId', cityId.toString());
    }
    if (clubId !== null && clubId !== undefined && Number(clubId) > 0) {
      params.append('clubId', clubId.toString());
    }
    if (includeFinished) {
      params.append('includeFinished', 'true');
    }

    const queryString = params.toString();
    const url = queryString ? `/api/tournaments/schedule?${queryString}` : '/api/tournaments/schedule';
    const response = await apiClient.get<Tournament[]>(url, { timeout: 35000 });
    return response.data;
  },

  async getTournament(id: number): Promise<TournamentDetail> {
    const response = await apiClient.get<TournamentDetail>(`/api/tournaments/${id}`, { timeout: 35000 });
    return response.data;
  },

  async getMyTournaments(): Promise<Tournament[]> {
    const response = await apiClient.get<Tournament[]>('/api/tournaments/my', { timeout: 35000 });
    return response.data;
  },

  async register(
    tournamentId: number, 
    profile?: { vkId?: string; firstName?: string; lastName?: string; avatarUrl?: string }
  ): Promise<{ message: string }> {
    const response = await apiClient.post<{ message: string }>('/api/tournaments/register', {
      tournamentId,
      ...profile,
    }, { timeout: 45000 });
    return response.data;
  },

  async unregister(tournamentId: number, vkId?: string): Promise<{ message: string }> {
    const response = await apiClient.post<{ message: string }>('/api/tournaments/unregister', {
      tournamentId,
      vkId,
    }, { timeout: 45000 });
    return response.data;
  },

  async createTournament(data: CreateTournamentRequest): Promise<Tournament> {
    const response = await apiClient.post<Tournament>('/api/tournaments', data, { timeout: 45000 });
    return response.data;
  },

  async updateTournament(id: number, data: UpdateTournamentRequest): Promise<TournamentDetail> {
    const response = await apiClient.put<TournamentDetail>(`/api/tournaments/${id}`, data, { timeout: 15000 });
    return response.data;
  },

  async deleteTournament(id: number): Promise<{ message: string }> {
    const response = await apiClient.delete<{ message: string }>(`/api/tournaments/${id}`, { timeout: 35000 });
    return response.data;
  },

  async cancelTournament(id: number): Promise<{ message: string }> {
    const response = await apiClient.post<{ message: string }>(`/api/tournaments/${id}/cancel`, {}, { timeout: 45000 });
    return response.data;
  },
};
