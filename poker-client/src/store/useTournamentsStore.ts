import { create } from 'zustand';
import axios from 'axios';
import type { Tournament, TournamentDetail, UpdateTournamentRequest } from '../types';
import { tournamentsApi } from '../api/tournamentsApi';
import { citiesApi } from '../api/citiesApi';
import { useUserStore } from './useUserStore';

interface TournamentsState {
  tournaments: Tournament[];
  myTournaments: Tournament[];
  selectedTournament: TournamentDetail | null;
  editingTournament: Tournament | TournamentDetail | null;
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
  deleteTournament: (tournamentId: number) => Promise<boolean>;
  setEditingTournament: (tournament: Tournament | TournamentDetail | null) => void;
  updateTournament: (id: number, data: UpdateTournamentRequest) => Promise<boolean>;
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
  editingTournament: null,
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

    // Если город не выбран и не сохранен в localStorage, по умолчанию ставим «Пермь»
    if ((targetCityId === undefined || targetCityId === null) && typeof window !== 'undefined') {
      const savedCityId = localStorage.getItem('poker_selected_city_id');
      if (savedCityId && savedCityId !== 'all') {
        const parsed = Number(savedCityId);
        if (!isNaN(parsed) && parsed > 0) {
          targetCityId = parsed;
        }
      } else if (!savedCityId) {
        try {
          const cities = await citiesApi.getCities();
          const perm = cities.find((c) => c.name.toLowerCase().includes('пермь')) || cities[0];
          if (perm) {
            targetCityId = perm.id;
            useUserStore.getState().setSelectedCity(perm.id, perm.name);
          }
        } catch {
          // ignore
        }
      }
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
      let detail: TournamentDetail | null = null;
      try {
        detail = await tournamentsApi.getTournament(tournamentId);
      } catch {
        // Игнорируем сетевой сбой при получении деталей, так как запись в БД уже прошла
      }

      // Немедленно реактивно обновляем и tournaments, и myTournaments
      set((state) => {
        const updatedTournaments = state.tournaments.map((t) =>
          t.id === tournamentId
            ? { ...t, isUserRegistered: true, registeredCount: t.registeredCount + 1 }
            : t
        );
        const registeredTour = updatedTournaments.find((t) => t.id === tournamentId) || (detail ? {
          ...detail,
          isUserRegistered: true,
        } : (state.selectedTournament?.id === tournamentId ? {
          ...state.selectedTournament,
          isUserRegistered: true,
          registeredCount: state.selectedTournament.registeredCount + 1,
        } : null));

        const existingMyIndex = state.myTournaments.findIndex((t) => t.id === tournamentId);
        const updatedMyTournaments = existingMyIndex >= 0
          ? state.myTournaments.map((t, idx) =>
              idx === existingMyIndex ? { ...t, isUserRegistered: true } : t
            )
          : registeredTour
          ? [...state.myTournaments, { ...registeredTour, isUserRegistered: true }]
          : state.myTournaments;

        return {
          selectedTournament: detail || (state.selectedTournament?.id === tournamentId ? {
            ...state.selectedTournament,
            isUserRegistered: true,
            registeredCount: state.selectedTournament.registeredCount + 1,
          } : state.selectedTournament),
          tournaments: updatedTournaments,
          myTournaments: updatedMyTournaments,
          actionError: null,
        };
      });

      await get().fetchMyTournaments();
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
      let detail: TournamentDetail | null = null;
      try {
        detail = await tournamentsApi.getTournament(tournamentId);
      } catch {
        // Игнорируем сетевой сбой при получении деталей, так как отмена в БД уже прошла
      }
      set((state) => ({
        selectedTournament: detail || (state.selectedTournament?.id === tournamentId ? {
          ...state.selectedTournament,
          isUserRegistered: false,
          registeredCount: Math.max(0, state.selectedTournament.registeredCount - 1),
        } : state.selectedTournament),
        tournaments: state.tournaments.map((t) =>
          t.id === tournamentId
            ? { ...t, isUserRegistered: false, registeredCount: Math.max(0, t.registeredCount - 1) }
            : t
        ),
        myTournaments: state.myTournaments.filter((t) => t.id !== tournamentId),
        actionError: null,
      }));
      await get().fetchMyTournaments();
      return true;
    } catch (err: unknown) {
      console.error('Ошибка отмены записи на турнир:', err);
      set({ actionError: extractErrorMessage(err, 'Ошибка отмены записи на турнир') });
      return false;
    } finally {
      set({ isActionLoading: false });
    }
  },

  deleteTournament: async (tournamentId: number) => {
    set({ isActionLoading: true, actionError: null });
    try {
      await tournamentsApi.deleteTournament(tournamentId);
      set((state) => ({
        tournaments: state.tournaments.filter((t) => t.id !== tournamentId),
        myTournaments: state.myTournaments.filter((t) => t.id !== tournamentId),
        selectedTournament: state.selectedTournament?.id === tournamentId ? null : state.selectedTournament,
        isDetailModalOpen: state.selectedTournament?.id === tournamentId ? false : state.isDetailModalOpen,
        actionError: null,
      }));
      return true;
    } catch (err: unknown) {
      console.error('Ошибка удаления турнира:', err);
      set({ actionError: extractErrorMessage(err, 'Ошибка удаления турнира') });
      return false;
    } finally {
      set({ isActionLoading: false });
    }
  },

  setEditingTournament: (tournament) => set({ editingTournament: tournament }),

  updateTournament: async (id: number, data: UpdateTournamentRequest) => {
    set({ isActionLoading: true, actionError: null });
    try {
      const updated = await tournamentsApi.updateTournament(id, data);
      set((state) => ({
        tournaments: state.tournaments.map((t) => (t.id === id ? { ...t, ...updated } : t)),
        myTournaments: state.myTournaments.map((t) => (t.id === id ? { ...t, ...updated } : t)),
        selectedTournament:
          state.selectedTournament?.id === id
            ? { ...state.selectedTournament, ...updated }
            : state.selectedTournament,
        editingTournament:
          state.editingTournament?.id === id
            ? { ...state.editingTournament, ...updated }
            : state.editingTournament,
        actionError: null,
      }));
      return true;
    } catch (err: unknown) {
      console.error('Ошибка обновления турнира:', err);
      set({ actionError: extractErrorMessage(err, 'Ошибка обновления турнира') });
      return false;
    } finally {
      set({ isActionLoading: false });
    }
  },
}));
