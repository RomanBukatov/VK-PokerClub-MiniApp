import { apiClient } from './apiClient';
import type { City, Club } from '../types';

export const citiesApi = {
  async getCities(): Promise<City[]> {
    const response = await apiClient.get<City[]>('/api/cities');
    return response.data;
  },

  async getCityById(id: number): Promise<City> {
    const response = await apiClient.get<City>(`/api/cities/${id}`);
    return response.data;
  },

  async getClubs(cityId?: number): Promise<Club[]> {
    const params = cityId ? `?cityId=${cityId}` : '';
    const response = await apiClient.get<Club[]>(`/api/clubs${params}`);
    return response.data;
  },
};
