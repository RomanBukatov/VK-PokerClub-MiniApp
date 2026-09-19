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
  scheduleError: string | null;

  fetchSchedule: (cityId?: number | null | boolean, clubId?: number | null, showLoader?: boolean) => Promise<void>;
  fetchAdminSchedule: (cityId?: number | null, clubId?: number | null) => Promise<void>;
  fetchMyTournaments: () => Promise<void>;
  openDetail: (id: number) => Promise<void>;
  closeDetail: () => void;
  setStatusFilter: (filter: 'all' | 'open' | 'announced') => void;
  setActionError: (err: string | null) => void;
  setScheduleError: (err: string | null) => void;
  registerToTournament: (tournamentId: number) => Promise<boolean>;
  unregisterFromTournament: (tournamentId: number) => Promise<boolean>;
}

const extractErrorMessage = (err: unknown, defaultMessage: string): string => {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as { message?: string; title?: string; errors?: Record<string, string[] | string> } | undefined;
    let msg = data?.message || data?.title;
    if (data?.errors && typeof data.errors === 'object') {
      const firstKey = Object.keys(data.errors)[0];
      const firstErr = Array.isArray(data.errors[firstKey]) ? data.errors[firstKey][0] : data.errors[firstKey];
      if (firstErr) {
        msg = msg ? `${msg}: ${firstErr}` : String(firstErr);
      }
    }
    if (!msg && typeof err.response?.data === 'string' && !err.response.data.trim().startsWith('<')) {
      msg = err.response.data;
    }
    return msg || err.message || defaultMessage;
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
  scheduleError: null,

  fetchSchedule: async (cityIdOrShowLoader, clubId, showLoader) => {
    let targetCityId: number | null | undefined;
    let targetClubId: number | null | undefined = clubId;
    let shouldShowLoader: boolean;

    if (typeof showLoader === 'boolean') {
      shouldShowLoader = showLoader;
    } else if (typeof cityIdOrShowLoader === 'boolean') {
      shouldShowLoader = cityIdOrShowLoader;
    } else if (cityIdOrShowLoader === undefined) {
      shouldShowLoader = false;
    } else {
      shouldShowLoader = true;
    }

    if (typeof cityIdOrShowLoader === 'boolean' || cityIdOrShowLoader === undefined) {
      targetCityId = useUserStore.getState().selectedCityId;
      targetClubId = useUserStore.getState().selectedClubId;
    } else {
      targetCityId = cityIdOrShowLoader;
    }

    if (shouldShowLoader) {
      set({ isLoading: true, scheduleError: null });
    }
    try {
      const effectiveCityId = targetCityId && targetCityId > 0 ? targetCityId : undefined;
      const effectiveClubId = effectiveCityId ? (targetClubId && targetClubId > 0 ? targetClubId : undefined) : undefined;
      const data = await tournamentsApi.getSchedule(effectiveCityId, effectiveClubId, false);
      set({ tournaments: data, scheduleError: null });
    } catch (err) {
      console.error('Ошибка загрузки расписания турниров:', err);
      if (shouldShowLoader) {
        set({ scheduleError: extractErrorMessage(err, 'Не удалось загрузить расписание') });
      }
    } finally {
      if (shouldShowLoader) {
        set({ isLoading: false });
      }
    }
  },

  fetchAdminSchedule: async (cityId, clubId) => {
    set({ isLoading: true, scheduleError: null });
    try {
      // Если выбран null (Все города) или 0 — передаем undefined, чтобы API вернул турниры всех городов
      const effectiveCityId = cityId && cityId > 0 ? cityId : undefined;
      const effectiveClubId = effectiveCityId ? (clubId && clubId > 0 ? clubId : undefined) : undefined;
      const data = await tournamentsApi.getSchedule(effectiveCityId, effectiveClubId, true);
      set({ tournaments: data, scheduleError: null });
    } catch (err) {
      console.error('Ошибка загрузки турниров для управления:', err);
      set({ scheduleError: extractErrorMessage(err, 'Не удалось загрузить турниры для управления') });
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
  setScheduleError: (err) => set({ scheduleError: err }),

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
