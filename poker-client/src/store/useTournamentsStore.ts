import { create } from 'zustand';
import type { Tournament, TournamentDetail } from '../types';
import { tournamentsApi } from '../api/tournamentsApi';

interface TournamentsState {
  tournaments: Tournament[];
  myTournaments: Tournament[];
  selectedTournament: TournamentDetail | null;
  isLoading: boolean;
  isActionLoading: boolean;
  isDetailModalOpen: boolean;
  statusFilter: 'all' | 'open' | 'announced';

  fetchSchedule: (cityId?: number | null, clubId?: number | null) => Promise<void>;
  fetchMyTournaments: () => Promise<void>;
  openDetail: (id: number) => Promise<void>;
  closeDetail: () => void;
  setStatusFilter: (filter: 'all' | 'open' | 'announced') => void;
  registerToTournament: (tournamentId: number) => Promise<boolean>;
  unregisterFromTournament: (tournamentId: number) => Promise<boolean>;
}

export const useTournamentsStore = create<TournamentsState>((set, get) => ({
  tournaments: [],
  myTournaments: [],
  selectedTournament: null,
  isLoading: false,
  isActionLoading: false,
  isDetailModalOpen: false,
  statusFilter: 'all',

  fetchSchedule: async (cityId, clubId) => {
    set({ isLoading: true });
    try {
      const data = await tournamentsApi.getSchedule(cityId ?? undefined, clubId ?? undefined);
      set({ tournaments: data });
    } catch (err) {
      console.error('Ошибка загрузки расписания турниров:', err);
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
    set({ isActionLoading: true });
    try {
      const detail = await tournamentsApi.getTournament(id);
      set({ selectedTournament: detail, isDetailModalOpen: true });
    } catch (err) {
      console.error('Ошибка загрузки деталей турнира:', err);
    } finally {
      set({ isActionLoading: false });
    }
  },

  closeDetail: () => set({ isDetailModalOpen: false, selectedTournament: null }),

  setStatusFilter: (filter) => set({ statusFilter: filter }),

  registerToTournament: async (tournamentId: number) => {
    set({ isActionLoading: true });
    try {
      await tournamentsApi.register(tournamentId);
      const detail = await tournamentsApi.getTournament(tournamentId);
      set((state) => ({
        selectedTournament: detail,
        tournaments: state.tournaments.map((t) =>
          t.id === tournamentId
            ? { ...t, isUserRegistered: true, registeredCount: t.registeredCount + 1 }
            : t
        ),
      }));
      get().fetchMyTournaments();
      return true;
    } catch (err) {
      console.error('Ошибка записи на турнир:', err);
      return false;
    } finally {
      set({ isActionLoading: false });
    }
  },

  unregisterFromTournament: async (tournamentId: number) => {
    set({ isActionLoading: true });
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
      }));
      get().fetchMyTournaments();
      return true;
    } catch (err) {
      console.error('Ошибка отмены записи на турнир:', err);
      return false;
    } finally {
      set({ isActionLoading: false });
    }
  },
}));
