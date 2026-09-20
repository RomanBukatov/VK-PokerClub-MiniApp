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

describe('useTournamentsStore registration & deletion', () => {
  const dummyTournament: Tournament = {
    id: 10,
    title: 'Sunday DeepStack',
    buyIn: 2500,
    startTime: new Date().toISOString(),
    status: TournamentStatus.RegistrationOpen,
    maxSeats: 30,
    registeredCount: 5,
    isUserRegistered: false,
    clubId: 1,
  };

  const dummyDetail = {
    ...dummyTournament,
    clubAddress: 'Монастырская 59',
    participants: [],
  };

  it('registerToTournament immediately updates tournaments and myTournaments reactively', async () => {
    const origRegister = tournamentsApi.register;
    const origGetTournament = tournamentsApi.getTournament;
    const origGetMyTournaments = tournamentsApi.getMyTournaments;

    useTournamentsStore.setState({
      tournaments: [dummyTournament],
      myTournaments: [],
      selectedTournament: dummyDetail,
    });

    tournamentsApi.register = mock(async () => ({ message: 'Успешно' }));
    tournamentsApi.getTournament = mock(async () => ({
      ...dummyDetail,
      registeredCount: 6,
      isUserRegistered: true,
    }));
    tournamentsApi.getMyTournaments = mock(async () => [
      { ...dummyTournament, isUserRegistered: true, registeredCount: 6 },
    ]);

    try {
      const ok = await useTournamentsStore.getState().registerToTournament(10);
      expect(ok).toBe(true);

      const state = useTournamentsStore.getState();
      expect(state.tournaments[0].isUserRegistered).toBe(true);
      expect(state.tournaments[0].registeredCount).toBe(6);
      expect(state.myTournaments.length).toBe(1);
      expect(state.myTournaments[0].id).toBe(10);
      expect(state.myTournaments[0].isUserRegistered).toBe(true);
    } finally {
      tournamentsApi.register = origRegister;
      tournamentsApi.getTournament = origGetTournament;
      tournamentsApi.getMyTournaments = origGetMyTournaments;
    }
  });

  it('unregisterFromTournament immediately removes tournament from myTournaments and updates state', async () => {
    const origUnregister = tournamentsApi.unregister;
    const origGetTournament = tournamentsApi.getTournament;
    const origGetMyTournaments = tournamentsApi.getMyTournaments;

    useTournamentsStore.setState({
      tournaments: [{ ...dummyTournament, isUserRegistered: true, registeredCount: 6 }],
      myTournaments: [{ ...dummyTournament, isUserRegistered: true, registeredCount: 6 }],
      selectedTournament: { ...dummyDetail, isUserRegistered: true, registeredCount: 6 },
    });

    tournamentsApi.unregister = mock(async () => ({ message: 'Успешно' }));
    tournamentsApi.getTournament = mock(async () => ({
      ...dummyDetail,
      registeredCount: 5,
      isUserRegistered: false,
    }));
    tournamentsApi.getMyTournaments = mock(async () => []);

    try {
      const ok = await useTournamentsStore.getState().unregisterFromTournament(10);
      expect(ok).toBe(true);

      const state = useTournamentsStore.getState();
      expect(state.tournaments[0].isUserRegistered).toBe(false);
      expect(state.tournaments[0].registeredCount).toBe(5);
      expect(state.myTournaments.length).toBe(0);
    } finally {
      tournamentsApi.unregister = origUnregister;
      tournamentsApi.getTournament = origGetTournament;
      tournamentsApi.getMyTournaments = origGetMyTournaments;
    }
  });

  it('deleteTournament removes tournament from tournaments and myTournaments', async () => {
    const origDelete = tournamentsApi.deleteTournament;

    useTournamentsStore.setState({
      tournaments: [{ ...dummyTournament, id: 99 }],
      myTournaments: [{ ...dummyTournament, id: 99 }],
      selectedTournament: { ...dummyDetail, id: 99 },
      isDetailModalOpen: true,
    });

    tournamentsApi.deleteTournament = mock(async () => ({ message: 'Удален' }));

    try {
      const ok = await useTournamentsStore.getState().deleteTournament(99);
      expect(ok).toBe(true);

      const state = useTournamentsStore.getState();
      expect(state.tournaments.find((t) => t.id === 99)).toBeUndefined();
      expect(state.myTournaments.find((t) => t.id === 99)).toBeUndefined();
      expect(state.selectedTournament).toBeNull();
      expect(state.isDetailModalOpen).toBe(false);
    } finally {
      tournamentsApi.deleteTournament = origDelete;
    }
  });
});

describe('useTournamentsStore tournament editing', () => {
  const dummyTournament: Tournament = {
    id: 50,
    title: 'Original Title',
    buyIn: 1000,
    startTime: '2026-09-25T19:00:00.000Z',
    status: TournamentStatus.RegistrationOpen,
    maxSeats: 30,
    registeredCount: 3,
    isUserRegistered: true,
    clubId: 1,
  };

  const dummyDetail = {
    ...dummyTournament,
    clubAddress: 'Монастырская 59',
    participants: [],
  };

  it('setEditingTournament correctly sets and clears editingTournament', () => {
    useTournamentsStore.getState().setEditingTournament(dummyTournament);
    expect(useTournamentsStore.getState().editingTournament).toEqual(dummyTournament);

    useTournamentsStore.getState().setEditingTournament(null);
    expect(useTournamentsStore.getState().editingTournament).toBeNull();
  });

  it('updateTournament updates tournaments, myTournaments, selectedTournament and editingTournament', async () => {
    const origUpdate = tournamentsApi.updateTournament;

    useTournamentsStore.setState({
      tournaments: [dummyTournament],
      myTournaments: [dummyTournament],
      selectedTournament: dummyDetail,
      editingTournament: dummyTournament,
    });

    const updatedDetail = {
      ...dummyDetail,
      title: 'Updated Title',
      buyIn: 2000,
      maxSeats: 50,
    };

    tournamentsApi.updateTournament = mock(async () => updatedDetail);

    try {
      const ok = await useTournamentsStore.getState().updateTournament(50, {
        title: 'Updated Title',
        buyIn: 2000,
        maxSeats: 50,
      });

      expect(ok).toBe(true);

      const state = useTournamentsStore.getState();
      expect(state.tournaments[0].title).toBe('Updated Title');
      expect(state.tournaments[0].buyIn).toBe(2000);
      expect(state.tournaments[0].maxSeats).toBe(50);
      expect(state.myTournaments[0].title).toBe('Updated Title');
      expect(state.selectedTournament?.title).toBe('Updated Title');
      expect(state.editingTournament?.title).toBe('Updated Title');
      expect(state.actionError).toBeNull();
    } finally {
      tournamentsApi.updateTournament = origUpdate;
    }
  });

  it('updateTournament sets actionError on failure', async () => {
    const origUpdate = tournamentsApi.updateTournament;

    useTournamentsStore.setState({
      tournaments: [dummyTournament],
      actionError: null,
    });

    tournamentsApi.updateTournament = mock(async () => {
      throw new Error('Network error');
    });

    try {
      const ok = await useTournamentsStore.getState().updateTournament(50, {
        title: 'Fail Title',
      });

      expect(ok).toBe(false);
      const state = useTournamentsStore.getState();
      expect(state.actionError).toBe('Network error');
    } finally {
      tournamentsApi.updateTournament = origUpdate;
    }
  });
});

