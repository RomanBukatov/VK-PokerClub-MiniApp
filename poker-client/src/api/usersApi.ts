import { apiClient } from './apiClient';
import type { UserProfile, UpdateProfilePayload, PublicUserProfile } from '../types';

export const usersApi = {
  async getMe(): Promise<UserProfile> {
    const response = await apiClient.get<UserProfile>('/api/users/me');
    return response.data;
  },

  async getPublicProfile(id: number | string): Promise<PublicUserProfile> {
    const response = await apiClient.get<PublicUserProfile>(`/api/users/${id}/public-profile`);
    return response.data;
  },

  async updateProfile(payload: UpdateProfilePayload): Promise<UserProfile> {
    const response = await apiClient.post<UserProfile>('/api/users/profile', payload);
    return response.data;
  },

  async acceptTerms(): Promise<{ success: boolean; acceptedTermsAt: string; message: string }> {
    const response = await apiClient.post<{ success: boolean; acceptedTermsAt: string; message: string }>('/api/users/accept-terms');
    return response.data;
  },
};

