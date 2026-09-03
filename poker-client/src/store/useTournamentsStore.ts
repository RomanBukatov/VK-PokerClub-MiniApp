import { create } from 'zustand';
import axios from 'axios';
import type { Tournament, TournamentDetail } from '../types';
import { tournamentsApi } from '../api/tournamentsApi';
import { useUserStore } from './useUserStore';

interface TournamentsState {
  tournaments: Tournament[];
  myTournaments: Tournament[];
  selectedTournament: TournamentDetail | null;
  isLoading: boolean;
  isActionLoading: boolean;
  isDetailModalOpen: boolean;
  statusFilter: 'all' | 'open' | 'announced';
  actionError: string | null;

  fetchSchedule: (cityId?: number | null, clubId?: number | null) => Promise<void>;
  fetchAdminSchedule: (cityId?: number | null, clubId?: number | null) => Promise<void>;
  fetchMyTournaments: () => Promise<void>;
  openDetail: (id: number) => Promise<void>;
  closeDetail: () => void;
  setStatusFilter: (filter: 'all' | 'open' | 'announced') => void;
  setActionError: (err: string | null) => void;
  registerToTournament: (tournamentId: number) => Promise<boolean>;
  unregisterFromTournament: (tournamentId: number) => Promise<boolean>;
}

const extractErrorMessage = (err: unknown, defaultMessage: string): string => {
  if (axios.isAxiosError(err)) {
    return (err.response?.data as { message?: string } | undefined)?.message || err.message || defaultMessage;
  }
  if (err instanceof Error) {
    return err.message;
  }
  return defaultMessage;
};

export const useTournamentsStore = create<TournamentsState>((set, get) => ({
  tournaments: [],
  myTournaments: [],
  selectedTournament: null,
  isLoading: false,
  isActionLoading: false,
  isDetailModalOpen: false,
  statusFilter: 'all',
  actionError: null,

  fetchSchedule: async (cityId, clubId) => {
    set({ isLoading: true });
    try {
      const data = await tournamentsApi.getSchedule(cityId ?? undefined, clubId ?? undefined, false);
      set({ tournaments: data });
    } catch (err) {
      console.error('Ошибка загрузки расписания турниров:', err);
    } finally {
      set({ isLoading: false });
    }
  },

  fetchAdminSchedule: async (cityId, clubId) => {
    set({ isLoading: true });
    try {
      const data = await tournamentsApi.getSchedule(cityId ?? undefined, clubId ?? undefined, true);
      set({ tournaments: data });
    } catch (err) {
      console.error('Ошибка загрузки турниров для управления:', err);
    } finally {
      set({ isLoading: false });
    }
  },

  fetchMyTournaments: async () => {
    try {
      const data = await tournamentsApi.getMyTournaments();
      set({ myTournaments: data });
    } catch (err) {
      console.error('Ошибка загрузки моих турниров:', err);
    }
  },

  openDetail: async (id: number) => {
    set({ isActionLoading: true, actionError: null });
    try {
      const detail = await tournamentsApi.getTournament(id);
      set({ selectedTournament: detail, isDetailModalOpen: true, actionError: null });
    } catch (err: unknown) {
      console.error('Ошибка загрузки деталей турнира:', err);
      set({ actionError: extractErrorMessage(err, 'Ошибка загрузки деталей турнира') });
    } finally {
      set({ isActionLoading: false });
    }
  },

  closeDetail: () => set({ isDetailModalOpen: false, selectedTournament: null, actionError: null }),

  setStatusFilter: (filter) => set({ statusFilter: filter }),

  setActionError: (err) => set({ actionError: err }),

  registerToTournament: async (tournamentId: number) => {
    set({ isActionLoading: true, actionError: null });
    try {
      const vkUser = useUserStore.getState().vkUser;
      await tournamentsApi.register(tournamentId, {
        vkId: vkUser?.id.toString(),
        firstName: vkUser?.first_name,
        lastName: vkUser?.last_name,
        avatarUrl: vkUser?.photo_200 || vkUser?.photo_100,
      });
      const detail = await tournamentsApi.getTournament(tournamentId);
      set((state) => ({
        selectedTournament: detail,
        tournaments: state.tournaments.map((t) =>
          t.id === tournamentId
            ? { ...t, isUserRegistered: true, registeredCount: t.registeredCount + 1 }
            : t
        ),
        actionError: null,
      }));
      get().fetchMyTournaments();
      return true;
    } catch (err: unknown) {
      console.error('Ошибка записи на турнир:', err);
      set({ actionError: extractErrorMessage(err, 'Ошибка записи на турнир') });
      return false;
    } finally {
      set({ isActionLoading: false });
    }
  },

  unregisterFromTournament: async (tournamentId: number) => {
    set({ isActionLoading: true, actionError: null });
    try {
      await tournamentsApi.unregister(tournamentId);
      const detail = await tournamentsApi.getTournament(tournamentId);
      set((state) => ({
        selectedTournament: detail,
        tournaments: state.tournaments.map((t) =>
          t.id === tournamentId
            ? { ...t, isUserRegistered: false, registeredCount: Math.max(0, t.registeredCount - 1) }
            : t
        ),
        actionError: null,
      }));
      get().fetchMyTournaments();
      return true;
    } catch (err: unknown) {
      console.error('Ошибка отмены записи на турнир:', err);
      set({ actionError: extractErrorMessage(err, 'Ошибка отмены записи на турнир') });
      return false;
    } finally {
      set({ isActionLoading: false });
    }
  },
}));
