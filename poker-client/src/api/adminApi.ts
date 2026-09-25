import { apiClient } from './apiClient';
import type { TournamentDetail, UpdateTournamentRequest } from '../types';

export interface SyncSheetsResponse {
  success: boolean;
  message: string;
  totalProcessed: number;
  updatedCount: number;
  createdCount: number;
}

export const adminApi = {
  async syncSheets(): Promise<SyncSheetsResponse> {
    const response = await apiClient.post<SyncSheetsResponse>(
      '/api/admin/sync-sheets',
      {},
      { timeout: 60000 }
    );
    return response.data;
  },

  async updateTournament(id: number, data: UpdateTournamentRequest): Promise<TournamentDetail> {
    const response = await apiClient.put<TournamentDetail>(
      `/api/tournaments/${id}`,
      data,
      { timeout: 15000 }
    );
    return response.data;
  },
};
