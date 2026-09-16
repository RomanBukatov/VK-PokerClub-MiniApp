export const TournamentStatus = {
  Announced: 0,
  RegistrationOpen: 1,
  Running: 2,
  Finished: 3,
  Canceled: 4,
} as const;
export type TournamentStatus = typeof TournamentStatus[keyof typeof TournamentStatus];

export const RegStatus = {
  Active: 0,
  Canceled: 1,
  Played: 2,
} as const;
export type RegStatus = typeof RegStatus[keyof typeof RegStatus];

export interface City {
  id: number;
  name: string;
  slug: string;
  isActive: boolean;
  activeClubsCount: number;
}

export interface Club {
  id: number;
  cityId: number;
  name: string;
  address?: string;
  isActive: boolean;
  cityName?: string;
}

export interface RegisteredPlayer {
  userId: number;
  vkId: string;
  firstName?: string;
  lastName?: string;
  avatarUrl?: string;
  totalRating: number;
  registeredAt: string;
  pointsEarned?: number;
}

export interface Tournament {
  id: number;
  title: string;
  format?: string;
  buyIn: number;
  description?: string;
  maxSeats: number;
  startTime: string;
  status: TournamentStatus;
  clubId: number;
  clubName?: string;
  clubAddress?: string;
  cityName?: string;
  registeredCount: number;
  isUserRegistered: boolean;
  startingChips?: number;
}

export interface TournamentDetail extends Tournament {
  participants: RegisteredPlayer[];
}

export interface LeaderboardEntry {
  rank: number;
  id: number;
  vkId: string;
  firstName?: string;
  lastName?: string;
  avatarUrl?: string;
  totalRating: number;
}

export interface VkUser {
  id: number;
  first_name: string;
  last_name: string;
  photo_200?: string;
  photo_100?: string;
  city?: { id: number; title: string };
  isAdmin?: boolean;
}

export type TabType = 'schedule' | 'leaderboard' | 'profile' | 'admin-tournaments' | 'admin-create';
export type AppTab = TabType;

export interface CreateTournamentRequest {
  title: string;
  clubId?: number;
  cityId?: number | null;
  address?: string;
  format?: string;
  buyIn: number;
  maxSeats: number;
  startTime: string;
  description?: string;
  startingChips?: number;
  blindLevelMinutes?: number;
}

export interface RegisterPlayerPayload {
  tournamentId: number;
  vkId?: string;
  firstName?: string;
  lastName?: string;
  avatarUrl?: string;
}

export interface UserProfile {
  id: number;
  vkId: string;
  firstName?: string;
  lastName?: string;
  fullName?: string;
  nickname?: string;
  phoneNumber?: string;
  clubCardId?: string;
  avatarUrl?: string;
  totalRating: number;
  status: 'Newbie' | 'Fish' | 'Reg' | 'Pro' | string;
  acceptedTermsAt?: string | null;
  tournamentsPlayed: number;
  winsCount: number;
  top3Count: number;
  top10Count: number;
  knockoutsCount: number;
  avgPlace: number;
  createdAt: string;
}

export interface UpdateProfilePayload {
  nickname?: string;
  fullName?: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  clubCardId?: string;
  avatarUrl?: string;
  acceptedTerms?: boolean;
  acceptedTermsAt?: string;
}

export interface Achievement {
  id: string;
  icon: string;
  title: string;
  description: string;
  current: number;
  target: number;
  isUnlocked: boolean;
  progressPercent: number;
}

