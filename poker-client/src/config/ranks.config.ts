export interface RankConfig {
  level: number;
  name: string;
  minRating: number;
  icon: string;
  gradient: string;
  textColor: string;
  borderColor: string;
  badgeBg: string;
}

export const RANKS: RankConfig[] = [
  {
    level: 15,
    name: 'Новичок',
    minRating: 0,
    icon: '🎟️',
    gradient: 'from-slate-600 to-slate-800',
    textColor: 'text-slate-200',
    borderColor: 'border-slate-500/40',
    badgeBg: 'bg-slate-700/60',
  },
  {
    level: 14,
    name: 'Игрок',
    minRating: 300,
    icon: '🎲',
    gradient: 'from-blue-700 to-indigo-900',
    textColor: 'text-blue-200',
    borderColor: 'border-blue-500/40',
    badgeBg: 'bg-blue-800/60',
  },
  {
    level: 13,
    name: 'Претендент',
    minRating: 600,
    icon: '🃏',
    gradient: 'from-teal-700 to-cyan-900',
    textColor: 'text-teal-200',
    borderColor: 'border-teal-500/40',
    badgeBg: 'bg-teal-800/60',
  },
  {
    level: 12,
    name: 'Регуляр',
    minRating: 800,
    icon: '♣️',
    gradient: 'from-emerald-700 to-green-950',
    textColor: 'text-emerald-200',
    borderColor: 'border-emerald-500/40',
    badgeBg: 'bg-emerald-800/60',
  },
  {
    level: 11,
    name: 'Тактик',
    minRating: 1100,
    icon: '♟️',
    gradient: 'from-sky-700 to-indigo-950',
    textColor: 'text-sky-200',
    borderColor: 'border-sky-500/40',
    badgeBg: 'bg-sky-800/60',
  },
  {
    level: 10,
    name: 'Стратег',
    minRating: 1400,
    icon: '🫀',
    gradient: 'from-rose-800 to-purple-950',
    textColor: 'text-rose-200',
    borderColor: 'border-rose-500/40',
    badgeBg: 'bg-rose-800/60',
  },
  {
    level: 9,
    name: 'Профи',
    minRating: 1800,
    icon: '♠️',
    gradient: 'from-amber-600 via-amber-700 to-yellow-900',
    textColor: 'text-amber-200',
    borderColor: 'border-amber-500/40',
    badgeBg: 'bg-amber-800/60',
  },
  {
    level: 8,
    name: 'Эксперт',
    minRating: 2100,
    icon: '🎩',
    gradient: 'from-violet-700 to-purple-950',
    textColor: 'text-violet-200',
    borderColor: 'border-violet-500/40',
    badgeBg: 'bg-violet-800/60',
  },
  {
    level: 7,
    name: 'Мастер',
    minRating: 2600,
    icon: '🎯',
    gradient: 'from-red-600 to-amber-900',
    textColor: 'text-red-200',
    borderColor: 'border-red-500/40',
    badgeBg: 'bg-red-800/60',
  },
  {
    level: 6,
    name: 'Грандмастер',
    minRating: 3100,
    icon: '🏆',
    gradient: 'from-[#c39a44] to-[#785b1a]',
    textColor: 'text-[#ffd700]',
    borderColor: 'border-[#c39a44]/50',
    badgeBg: 'bg-[#916b1e]/60',
  },
  {
    level: 5,
    name: 'Элита',
    minRating: 3600,
    icon: '💎',
    gradient: 'from-cyan-500 to-blue-800',
    textColor: 'text-cyan-200',
    borderColor: 'border-cyan-400/50',
    badgeBg: 'bg-cyan-700/60',
  },
  {
    level: 4,
    name: 'Легенда',
    minRating: 5000,
    icon: '👑',
    gradient: 'from-fuchsia-600 via-purple-700 to-amber-600',
    textColor: 'text-fuchsia-200',
    borderColor: 'border-fuchsia-500/50',
    badgeBg: 'bg-purple-800/60',
  },
  {
    level: 3,
    name: 'Чемпион',
    minRating: 6500,
    icon: '🔱',
    gradient: 'from-yellow-500 via-amber-600 to-orange-700',
    textColor: 'text-yellow-200',
    borderColor: 'border-yellow-400/50',
    badgeBg: 'bg-yellow-700/60',
  },
  {
    level: 2,
    name: 'Титан',
    minRating: 10000,
    icon: '⚡',
    gradient: 'from-blue-600 via-indigo-600 to-violet-800',
    textColor: 'text-indigo-200',
    borderColor: 'border-indigo-400/60',
    badgeBg: 'bg-indigo-800/60',
  },
  {
    level: 1,
    name: 'Икона Монте-Карло',
    minRating: 15000,
    icon: '⚓',
    gradient: 'from-[#ffd700] via-[#c39a44] to-[#0e2a20]',
    textColor: 'text-[#ffd700]',
    borderColor: 'border-[#ffd700]/70',
    badgeBg: 'bg-[#0a231b] border border-[#ffd700]/50',
  },
];

export interface NextRankInfo {
  level: number;
  name: string;
  minRating: number;
  icon: string;
}

export interface UserRankProgress {
  currentRank: RankConfig;
  nextRank: NextRankInfo;
  displayName: string;
  badgeText: string;
  pointsToNext: number;
  progressPercent: number;
  currentPoints: number;
  targetPoints: number;
  isPrestige: boolean;
  prestigeMultiplier: number;
}

export function getClubRankName(rating: number): string {
  const pts = Math.max(0, rating);
  if (pts > 15000) {
    const multiplier = Math.floor((pts - 1) / 15000) + 1;
    return `Икона МК x${multiplier}`;
  }

  for (let i = RANKS.length - 1; i >= 0; i--) {
    if (pts >= RANKS[i].minRating) {
      return RANKS[i].name;
    }
  }

  return 'Новичок';
}

export function getRankProgress(rating: number): UserRankProgress {
  const pts = Math.max(0, rating);

  if (pts > 15000) {
    const prestigeMultiplier = Math.floor((pts - 1) / 15000) + 1;
    const displayName = `Икона МК x${prestigeMultiplier}`;
    const badgeText = `x${prestigeMultiplier}`;
    const basePts = (prestigeMultiplier - 1) * 15000;
    const targetPts = prestigeMultiplier * 15000;
    const pointsToNext = Math.max(0, targetPts - pts);
    const progressPercent = Math.min(100, Math.max(0, Math.round(((pts - basePts) / 15000) * 100)));

    const iconRank = RANKS[RANKS.length - 1];
    return {
      currentRank: iconRank,
      nextRank: {
        level: 1,
        name: `Икона МК x${prestigeMultiplier + 1}`,
        minRating: targetPts,
        icon: '⚓',
      },
      displayName,
      badgeText,
      pointsToNext,
      progressPercent,
      currentPoints: pts,
      targetPoints: targetPts,
      isPrestige: true,
      prestigeMultiplier,
    };
  }

  if (pts === 15000) {
    const iconRank = RANKS[RANKS.length - 1];
    return {
      currentRank: iconRank,
      nextRank: {
        level: 1,
        name: 'Икона МК x2',
        minRating: 30000,
        icon: '⚓',
      },
      displayName: iconRank.name,
      badgeText: `${iconRank.level} LVL`,
      pointsToNext: 15000,
      progressPercent: 0,
      currentPoints: pts,
      targetPoints: 30000,
      isPrestige: false,
      prestigeMultiplier: 1,
    };
  }

  let currentIndex = 0;
  for (let i = RANKS.length - 1; i >= 0; i--) {
    if (pts >= RANKS[i].minRating) {
      currentIndex = i;
      break;
    }
  }

  const currentRank = RANKS[currentIndex];
  const nextRank = currentIndex < RANKS.length - 1 ? RANKS[currentIndex + 1] : RANKS[currentIndex];

  const targetPoints = nextRank.minRating;
  const basePoints = currentRank.minRating;
  const pointsToNext = Math.max(0, targetPoints - pts);
  const range = targetPoints - basePoints;
  const progressPercent = range > 0
    ? Math.min(100, Math.max(0, Math.round(((pts - basePoints) / range) * 100)))
    : 0;

  return {
    currentRank,
    nextRank,
    displayName: currentRank.name,
    badgeText: `${currentRank.level} LVL`,
    pointsToNext,
    progressPercent,
    currentPoints: pts,
    targetPoints,
    isPrestige: false,
    prestigeMultiplier: 1,
  };
}
