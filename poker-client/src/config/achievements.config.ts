import type { Achievement } from '../types';

export interface AchievementStats {
  tournamentsPlayed?: number | null;
  winsCount?: number | null;
  top3Count?: number | null;
  knockoutsCount?: number | null;
  totalRating?: number | null;
}

export function getAchievements(stats: AchievementStats): Achievement[] {
  const tournamentsPlayed = Math.max(0, stats.tournamentsPlayed ?? 0);
  const winsCount = Math.max(0, stats.winsCount ?? 0);
  const top3Count = Math.max(0, stats.top3Count ?? 0);
  const knockoutsCount = Math.max(0, stats.knockoutsCount ?? 0);
  const totalRating = Math.max(0, stats.totalRating ?? 0);

  return [
    {
      id: 'first_step',
      icon: '🎟️',
      title: 'Первый шаг',
      description: 'Сыграть 1 турнир',
      current: Math.min(tournamentsPlayed, 1),
      target: 1,
      isUnlocked: tournamentsPlayed >= 1,
      progressPercent: Math.min(100, Math.round((tournamentsPlayed / 1) * 100)),
    },
    {
      id: 'first_win',
      icon: '🏆',
      title: 'Первая победа',
      description: 'Выиграть 1 турнир',
      current: Math.min(winsCount, 1),
      target: 1,
      isUnlocked: winsCount >= 1,
      progressPercent: Math.min(100, Math.round((winsCount / 1) * 100)),
    },
    {
      id: 'on_fire',
      icon: '⚡',
      title: 'В ударе',
      description: '3 финиша в Топ-3',
      current: Math.min(top3Count, 3),
      target: 3,
      isUnlocked: top3Count >= 3,
      progressPercent: Math.min(100, Math.round((top3Count / 3) * 100)),
    },
    {
      id: 'veteran',
      icon: '🎖️',
      title: 'Ветеран клуба',
      description: 'Сыграть 15 турниров',
      current: Math.min(tournamentsPlayed, 15),
      target: 15,
      isUnlocked: tournamentsPlayed >= 15,
      progressPercent: Math.min(100, Math.round((tournamentsPlayed / 15) * 100)),
    },
    {
      id: 'bounty_hunter',
      icon: '🥊',
      title: 'Охотник за головами',
      description: 'Сделать 20 нокаутов',
      current: Math.min(knockoutsCount, 20),
      target: 20,
      isUnlocked: knockoutsCount >= 20,
      progressPercent: Math.min(100, Math.round((knockoutsCount / 20) * 100)),
    },
    {
      id: 'shark',
      icon: '🦈',
      title: 'Акула стола',
      description: 'Набрать 300 очков',
      current: Math.min(totalRating, 300),
      target: 300,
      isUnlocked: totalRating >= 300,
      progressPercent: Math.min(100, Math.round((totalRating / 300) * 100)),
    },
    {
      id: 'champion',
      icon: '👑',
      title: 'Чемпион',
      description: 'Одержать 3 победы',
      current: Math.min(winsCount, 3),
      target: 3,
      isUnlocked: winsCount >= 3,
      progressPercent: Math.min(100, Math.round((winsCount / 3) * 100)),
    },
    {
      id: 'grinder',
      icon: '🎯',
      title: 'Гриндер',
      description: 'Сыграть 30 турниров',
      current: Math.min(tournamentsPlayed, 30),
      target: 30,
      isUnlocked: tournamentsPlayed >= 30,
      progressPercent: Math.min(100, Math.round((tournamentsPlayed / 30) * 100)),
    },
    {
      id: 'executioner',
      icon: '💀',
      title: 'Палач',
      description: 'Сделать 50 нокаутов',
      current: Math.min(knockoutsCount, 50),
      target: 50,
      isUnlocked: knockoutsCount >= 50,
      progressPercent: Math.min(100, Math.round((knockoutsCount / 50) * 100)),
    },
    {
      id: 'table_terror',
      icon: '🔱',
      title: 'Гроза столов',
      description: 'Одержать 5 побед',
      current: Math.min(winsCount, 5),
      target: 5,
      isUnlocked: winsCount >= 5,
      progressPercent: Math.min(100, Math.round((winsCount / 5) * 100)),
    },
    {
      id: 'high_roller',
      icon: '💎',
      title: 'Хайроллер',
      description: 'Набрать 600 очков',
      current: Math.min(totalRating, 600),
      target: 600,
      isUnlocked: totalRating >= 600,
      progressPercent: Math.min(100, Math.round((totalRating / 600) * 100)),
    },
    {
      id: 'legend',
      icon: '⚓',
      title: 'Легенда Monte Carlo',
      description: 'Набрать 1000 очков',
      current: Math.min(totalRating, 1000),
      target: 1000,
      isUnlocked: totalRating >= 1000,
      progressPercent: Math.min(100, Math.round((totalRating / 1000) * 100)),
    },
  ];
}
