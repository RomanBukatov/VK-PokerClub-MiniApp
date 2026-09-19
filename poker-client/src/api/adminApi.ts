import { apiClient } from './apiClient';

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
};
