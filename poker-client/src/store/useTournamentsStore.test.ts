import { describe, it, expect, mock } from 'bun:test';
import { useTournamentsStore } from './useTournamentsStore';
import { tournamentsApi } from '../api/tournamentsApi';
import { TournamentStatus, type Tournament } from '../types';

describe('useTournamentsStore fetchSchedule silent refresh', () => {
  const dummyTournament: Tournament = {
    id: 1,
    title: 'Test Tournament',
    buyIn: 1000,
    startTime: new Date().toISOString(),
    status: TournamentStatus.RegistrationOpen,
    maxSeats: 30,
    registeredCount: 5,
  };

  it('fetchSchedule(false) performs silent refresh without setting isLoading', async () => {
    const originalGetSchedule = tournamentsApi.getSchedule;
    let wasLoadingDuringFetch = false;

    tournamentsApi.getSchedule = mock(async () => {
      wasLoadingDuringFetch = useTournamentsStore.getState().isLoading;
      return [dummyTournament];
    });

    try {
      await useTournamentsStore.getState().fetchSchedule(false);

      expect(wasLoadingDuringFetch).toBe(false);
      const state = useTournamentsStore.getState();
      expect(state.isLoading).toBe(false);
      expect(state.tournaments.length).toBe(1);
    } finally {
      tournamentsApi.getSchedule = originalGetSchedule;
    }
  });

  it('fetchSchedule() with no arguments performs silent refresh without setting isLoading', async () => {
    const originalGetSchedule = tournamentsApi.getSchedule;
    let wasLoadingDuringFetch = false;

    tournamentsApi.getSchedule = mock(async () => {
      wasLoadingDuringFetch = useTournamentsStore.getState().isLoading;
      return [dummyTournament];
    });

    try {
      await useTournamentsStore.getState().fetchSchedule();

      expect(wasLoadingDuringFetch).toBe(false);
      const state = useTournamentsStore.getState();
      expect(state.isLoading).toBe(false);
      expect(state.tournaments.length).toBe(1);
    } finally {
      tournamentsApi.getSchedule = originalGetSchedule;
    }
  });

  it('fetchSchedule(cityId, clubId) activates isLoading for normal user navigation', async () => {
    const originalGetSchedule = tournamentsApi.getSchedule;
    let wasLoadingDuringFetch = false;

    tournamentsApi.getSchedule = mock(async () => {
      wasLoadingDuringFetch = useTournamentsStore.getState().isLoading;
      return [dummyTournament];
    });

    try {
      await useTournamentsStore.getState().fetchSchedule(1, 2);

      expect(wasLoadingDuringFetch).toBe(true);
      const state = useTournamentsStore.getState();
      expect(state.isLoading).toBe(false);
      expect(state.tournaments.length).toBe(1);
    } finally {
      tournamentsApi.getSchedule = originalGetSchedule;
    }
  });
});
