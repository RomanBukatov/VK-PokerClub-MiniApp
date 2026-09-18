import React, { useEffect, useState } from 'react';
import { 
  Trophy, 
  Award, 
  Medal, 
  Crown, 
  Flame, 
  Target, 
  Fish, 
  Crosshair, 
  Lock, 
  CheckCircle2, 
  Edit3, 
  Calendar, 
  Clock, 
  MapPin, 
  ChevronRight, 
  Zap, 
  LogOut 
} from 'lucide-react';
import { useUserStore } from '../store/useUserStore';
import { useTournamentsStore } from '../store/useTournamentsStore';
import { useRatingsStore } from '../store/useRatingsStore';
import { formatChips, formatCurrency } from '../utils/formatters';
import { triggerHaptic } from '../utils/vkBridge';
import { TournamentStatus, type Achievement } from '../types';
import { getRankProgress } from '../config/ranks.config';

export const ProfilePanel: React.FC = () => {
  const { vkUser, profile, fetchProfile, setIsProfileModalOpen, logout } = useUserStore();
  const { myTournaments, fetchMyTournaments, openDetail } = useTournamentsStore();
  const { leaderboard, fetchLeaderboard } = useRatingsStore();

  const [tournamentsTab, setTournamentsTab] = useState<'upcoming' | 'history'>('upcoming');

  useEffect(() => {
    fetchProfile();
    fetchMyTournaments();
    fetchLeaderboard();
  }, [fetchProfile, fetchMyTournaments, fetchLeaderboard]);

  const userEntry = vkUser 
    ? leaderboard.find((u) => u.vkId === vkUser.id.toString())
    : null;

  const currentRating = profile?.totalRating ?? userEntry?.totalRating ?? 0;
  const tournamentsPlayed = profile?.tournamentsPlayed ?? myTournaments.length;
  const winsCount = profile?.winsCount ?? 0;
  const top3Count = profile?.top3Count ?? 0;
  const knockoutsCount = profile?.knockoutsCount ?? 0;
  const avgPlace = profile?.avgPlace && profile.avgPlace > 0 ? profile.avgPlace.toFixed(2) : '-';
  const winRate = tournamentsPlayed > 0 
    ? `${((winsCount / tournamentsPlayed) * 100).toFixed(1)}%` 
    : '0.0%';

  // Официальная 15-уровневая система рангов клуба Monte Carlo
  const rankProgress = getRankProgress(currentRating);

  // Система достижений (6-8 ачивок)
  const achievements: Achievement[] = [
    {
      id: 'first_win',
      icon: '🏆',
      title: 'Первая победа',
      description: 'Выиграть 1 турнир клуба',
      current: Math.min(winsCount, 1),
      target: 1,
      isUnlocked: winsCount >= 1,
      progressPercent: Math.min(100, Math.round((winsCount / 1) * 100)),
    },
    {
      id: 'veteran',
      icon: '🎖️',
      title: 'Ветеран',
      description: 'Сыграть 10 турниров',
      current: Math.min(tournamentsPlayed, 10),
      target: 10,
      isUnlocked: tournamentsPlayed >= 10,
      progressPercent: Math.min(100, Math.round((tournamentsPlayed / 10) * 100)),
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
      id: 'grinder',
      icon: '🎯',
      title: 'Гриндер',
      description: 'Сыграть 20 турниров',
      current: Math.min(tournamentsPlayed, 20),
      target: 20,
      isUnlocked: tournamentsPlayed >= 20,
      progressPercent: Math.min(100, Math.round((tournamentsPlayed / 20) * 100)),
    },
    {
      id: 'shark',
      icon: '🦈',
      title: 'Акула стола',
      description: 'Набрать более 300 очков',
      current: Math.min(currentRating, 300),
      target: 300,
      isUnlocked: currentRating >= 300,
      progressPercent: Math.min(100, Math.round((currentRating / 300) * 100)),
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
  ];

  const unlockedCount = achievements.filter((a) => a.isUnlocked).length;

  const renderAchievementIcon = (id: string, isUnlocked: boolean) => {
    const iconClass = isUnlocked ? 'w-5 h-5 text-[#ffd700]' : 'w-5 h-5 text-[#c39a44]';
    switch (id) {
      case 'first_win':
        return <Trophy className={iconClass} />;
      case 'veteran':
        return <Medal className={iconClass} />;
      case 'champion':
        return <Crown className={iconClass} />;
      case 'on_fire':
        return <Flame className={iconClass} />;
      case 'grinder':
        return <Target className={iconClass} />;
      case 'shark':
        return <Fish className={iconClass} />;
      case 'bounty_hunter':
        return <Crosshair className={iconClass} />;
      default:
        return <Award className={iconClass} />;
    }
  };

  const upcomingTournaments = myTournaments.filter(
    (t) => t.isUserRegistered && t.status !== TournamentStatus.Finished && t.status !== TournamentStatus.Canceled
  );

  const finishedTournaments = myTournaments.filter(
    (t) => t.status === TournamentStatus.Finished
  );

  const getInitials = (name?: string) => {
    if (!name) return 'MC';
    const parts = name.trim().split(' ');
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }
    return name.slice(0, 2).toUpperCase();
  };

  const displayName = profile?.fullName || `${vkUser?.first_name || ''} ${vkUser?.last_name || ''}`.trim() || 'Игрок Monte Carlo';
  const displayNickname = profile?.nickname || (vkUser ? (vkUser.id ? `Player_${vkUser.id}` : 'Гость') : 'Player');
  const displayPhone = profile?.phoneNumber || 'Телефон не указан';
  const displayCardId = profile?.clubCardId ? `#${profile.clubCardId}` : null;

  return (
    <div className="px-5 pb-32 animate-fade-in space-y-5 text-white">
      {/* 1. ШАПКА ИГРОКА */}
      <div className="pt-2 p-5 rounded-3xl bg-gradient-to-b from-[#0e2a20] to-[#081c15] border border-white/10 shadow-2xl relative overflow-hidden">
        {/* Фоновые декоративные элементы */}
        <div className="absolute top-0 right-0 w-36 h-36 bg-[#c39a44]/10 rounded-full blur-3xl pointer-events-none" />

        <div className="flex items-start justify-between relative z-10 mb-3.5">
          <div className="flex items-center gap-3.5">
            {/* Аватар с инициалами / фото */}
            <div className="relative">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-[#2a4d3d] to-[#122e23] border-2 border-[#c39a44]/50 flex items-center justify-center font-black text-xl text-[#d8af56] overflow-hidden shadow-lg shadow-black/60 shrink-0">
                {profile?.avatarUrl || vkUser?.photo_200 ? (
                  <img src={profile?.avatarUrl || vkUser?.photo_200} alt="" className="w-full h-full object-cover" />
                ) : (
                  getInitials(displayName)
                )}
              </div>
              {/* Статус бейдж поверх аватара */}
              <div className={`absolute -bottom-1.5 -right-1.5 px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-wider bg-gradient-to-r ${rankProgress.currentRank.gradient} text-white shadow-md border ${rankProgress.currentRank.borderColor} flex items-center gap-1 max-w-[105px]`}>
                <span className="shrink-0">{rankProgress.currentRank.icon}</span>
                <span className="truncate">{rankProgress.isPrestige ? rankProgress.displayName : (rankProgress.currentRank.level === 1 ? 'Икона МК' : rankProgress.displayName)}</span>
              </div>
            </div>

            {/* Никнейм, ФИО и телефон */}
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-xl font-black text-white tracking-tight">
                  {displayNickname}
                </h2>
                {displayCardId ? (
                  <span className="px-2 py-0.5 rounded-lg bg-black/40 border border-white/10 text-[10px] font-bold text-[#c39a44]">
                    {displayCardId}
                  </span>
                ) : (
                  <button
                    type="button"
                    onClick={() => {
                      triggerHaptic('light');
                      setIsProfileModalOpen(true);
                    }}
                    className="px-2 py-0.5 rounded-lg bg-[#c39a44]/20 border border-[#c39a44]/40 hover:bg-[#c39a44]/30 text-[10px] font-bold text-[#ffd700] flex items-center gap-1 transition-all active:scale-95"
                    title="Привязать клубную карту"
                  >
                    + Привязать карту
                  </button>
                )}
              </div>
              <p className="text-xs font-semibold text-[#d1e0d7] mt-0.5">
                {displayName}
              </p>
              <p className="text-[11px] text-[#8fa89b]">
                {displayPhone}
              </p>
            </div>
          </div>

          {/* Кнопка редактирования профиля */}
          <button
            type="button"
            onClick={() => {
              triggerHaptic('light');
              setIsProfileModalOpen(true);
            }}
            className="p-2.5 rounded-2xl bg-white/5 hover:bg-white/10 border border-white/10 text-[#c39a44] transition-all active:scale-95 shadow-md"
            title="Редактировать анкету"
          >
            <Edit3 className="w-4 h-4" />
          </button>
        </div>

        {/* Рейтинг и клубный статус полоса */}
        <div className="pt-3 border-t border-white/10 flex items-center justify-between">
          <div>
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
              КЛУБНЫЙ РЕЙТИНГ (RPS)
            </div>
            <div className="text-2xl font-black text-[#c39a44] flex items-center gap-1.5 mt-0.5">
              <span>{currentRating.toLocaleString('ru-RU')}</span>
              <span className="text-xs font-bold text-[#8fa89b] uppercase">очков</span>
            </div>
          </div>

          <div className="text-right">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
              СЕЗОННЫЙ РАНГ
            </div>
            <div className="text-lg font-extrabold text-white mt-0.5">
              {userEntry ? `#${userEntry.rank} в клубе` : 'Участник'}
            </div>
          </div>
        </div>
      </div>

      {/* 2. СИСТЕМА РАНГОВ MONTE CARLO (15 УРОВНЕЙ & ПРЕСТИЖ) */}
      <div className="p-4 rounded-3xl bg-gradient-to-b from-[#0e2a20] to-[#071912] border border-[#c39a44]/30 shadow-2xl space-y-3 relative overflow-hidden">
        {/* Фоновое свечение ранга */}
        <div className="absolute top-0 right-0 w-32 h-32 bg-[#c39a44]/10 rounded-full blur-2xl pointer-events-none" />

        <div className="flex items-center justify-between relative z-10">
          <div className="flex items-center gap-2.5">
            <span className="text-2xl shrink-0 drop-shadow">{rankProgress.currentRank.icon}</span>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-base font-black text-white tracking-tight">
                  {rankProgress.displayName}
                </span>
                <span className={`px-2 py-0.5 rounded-md text-[10px] font-extrabold uppercase tracking-wide ${rankProgress.currentRank.badgeBg} ${rankProgress.currentRank.textColor} border ${rankProgress.currentRank.borderColor}`}>
                  {rankProgress.badgeText}
                </span>
              </div>
              <p className="text-[10px] text-[#8fa89b] mt-0.5">
                {rankProgress.isPrestige 
                  ? `Престиж Monte Carlo · ${rankProgress.displayName}`
                  : `Ранг Monte Carlo · Уровень ${rankProgress.currentRank.level} из 15`}
              </p>
            </div>
          </div>

          <div className="text-right">
            <span className="text-sm font-black text-[#ffd700]">
              {rankProgress.progressPercent}%
            </span>
          </div>
        </div>

        {/* Прогресс-бар ранга */}
        <div className="w-full bg-black/60 rounded-full h-2.5 border border-white/10 overflow-hidden p-0.5 shadow-inner">
          <div
            className={`h-full rounded-full transition-all duration-700 bg-gradient-to-r ${
              rankProgress.isPrestige
                ? 'from-[#ffd700] via-[#c39a44] to-[#ffd700]'
                : rankProgress.currentRank.gradient
            }`}
            style={{ width: `${Math.max(rankProgress.progressPercent, 4)}%` }}
          />
        </div>

        {/* Индикатор до следующего ранга:
            «Текущий уровень: Профи ➔ До ранга Эксперт осталось 300 очков» */}
        <div className="flex items-center justify-between text-[11px] font-bold text-[#d1e0d7] pt-0.5 relative z-10">
          <div className="flex items-center gap-1.5 flex-wrap">
            <span className="text-[#8fa89b]">Текущий уровень:</span>
            <span className="text-white font-extrabold">{rankProgress.displayName}</span>
            <span className="text-[#c39a44]">➔</span>
            <span className="text-[#8fa89b]">До ранга</span>
            <span className="text-[#ffd700] font-extrabold">{rankProgress.nextRank.name}</span>
            <span className="text-[#8fa89b]">осталось</span>
            <span className="text-[#ffd700] font-black">{rankProgress.pointsToNext.toLocaleString('ru-RU')}</span>
            <span className="text-[#8fa89b]">очков</span>
          </div>

          <div className="flex items-center gap-1 shrink-0 ml-2 text-[10px] text-[#8fa89b]">
            <span>{currentRating.toLocaleString('ru-RU')}</span>
            <span>/</span>
            <span>{rankProgress.targetPoints.toLocaleString('ru-RU')}</span>
          </div>
        </div>
      </div>

      {/* 3. МЕТРИКИ ИГРОКА (2x2 GRID) */}
      <div>
        <h3 className="text-sm font-extrabold uppercase tracking-wider text-[#8fa89b] mb-3 flex items-center gap-2">
          <Award className="w-4 h-4 text-[#c39a44]" />
          Метрики игрока
        </h3>

        <div className="grid grid-cols-2 gap-3">
          {/* AVG Позиция */}
          <div className="p-4 rounded-2xl bg-black/40 border border-white/10 shadow-lg">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
              AVG Позиция
            </div>
            <div className="text-2xl font-black text-white mt-1">
              {avgPlace}
            </div>
            <div className="text-[10px] text-[#606a66] mt-0.5">
              Среднее место за столом
            </div>
          </div>

          {/* ТОП-3 */}
          <div className="p-4 rounded-2xl bg-black/40 border border-white/10 shadow-lg">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
              ТОП-3 Финиши
            </div>
            <div className="text-2xl font-black text-[#d8af56] mt-1">
              {top3Count}
            </div>
            <div className="text-[10px] text-[#606a66] mt-0.5">
              Призовые места
            </div>
          </div>

          {/* Серия призов / Win Rate */}
          <div className="p-4 rounded-2xl bg-black/40 border border-white/10 shadow-lg">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
              Win Rate %
            </div>
            <div className="text-2xl font-black text-emerald-400 mt-1">
              {winRate}
            </div>
            <div className="text-[10px] text-[#606a66] mt-0.5">
              Процент побед
            </div>
          </div>

          {/* Победы */}
          <div className="p-4 rounded-2xl bg-black/40 border border-white/10 shadow-lg">
            <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
              Победы (1-е место)
            </div>
            <div className="text-2xl font-black text-white mt-1">
              {winsCount}
            </div>
            <div className="text-[10px] text-[#606a66] mt-0.5">
              {tournamentsPlayed} {tournamentsPlayed === 1 ? 'турнир сыгран' : tournamentsPlayed < 5 ? 'турнира сыграно' : 'турниров сыграно'}
            </div>
          </div>
        </div>
      </div>

      {/* 3. БЛОК «ДОСТИЖЕНИЯ» (ACHIEVEMENTS) */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-extrabold uppercase tracking-wider text-[#8fa89b] flex items-center gap-2">
            <Trophy className="w-4 h-4 text-[#c39a44]" />
            Достижения
          </h3>
          <span className="text-xs font-bold text-[#c39a44] px-2.5 py-0.5 rounded-full bg-[#c39a44]/15 border border-[#c39a44]/30">
            {unlockedCount} из {achievements.length}
          </span>
        </div>

        <div className="space-y-2.5">
          {achievements.map((ach) => (
            <div
              key={ach.id}
              className={`p-3.5 rounded-2xl border transition-all flex items-center gap-3.5 shadow-md ${
                ach.isUnlocked
                  ? 'bg-gradient-to-r from-[#0a231b] to-[#113527] border-[#c39a44]/50 shadow-[#c39a44]/10'
                  : 'bg-[#0a231b]/90 border-white/10'
              }`}
            >
              {/* Значок ачивки */}
              <div
                className={`w-11 h-11 rounded-2xl flex items-center justify-center shrink-0 shadow-inner ${
                  ach.isUnlocked
                    ? 'bg-gradient-to-br from-[#d8af56]/30 to-[#b38833]/20 border border-[#c39a44]/60 shadow-[0_0_12px_rgba(195,154,68,0.25)]'
                    : 'bg-black/50 border border-white/10'
                }`}
              >
                {renderAchievementIcon(ach.id, ach.isUnlocked)}
              </div>

              {/* Название, описание и прогресс-бар */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between">
                  <h4 className="text-xs font-black text-white truncate">
                    {ach.title}
                  </h4>
                  <div className="flex items-center gap-1 shrink-0 ml-2">
                    {ach.isUnlocked ? (
                      <span className="text-[10px] font-extrabold text-[#ffd700] flex items-center gap-1">
                        <CheckCircle2 className="w-3 h-3 text-[#ffd700]" />
                        Открыто
                      </span>
                    ) : (
                      <span className="text-[10px] font-bold text-[#8fa89b] flex items-center gap-1">
                        <Lock className="w-3 h-3 text-[#8fa89b]" />
                        {ach.current}/{ach.target}
                      </span>
                    )}
                  </div>
                </div>

                <p className="text-[11px] text-[#a4c9b7] truncate mt-0.5 font-normal">
                  {ach.description}
                </p>

                {/* Прогресс-бар */}
                <div className="w-full bg-white/10 rounded-full h-1.5 mt-2 overflow-hidden">
                  <div
                    className={`h-full transition-all duration-500 rounded-full ${
                      ach.isUnlocked
                        ? 'bg-gradient-to-r from-[#ffd700] to-[#c39a44]'
                        : 'bg-[#c39a44]/70'
                    }`}
                    style={{ width: `${Math.max(ach.progressPercent, ach.current > 0 ? 5 : 0)}%` }}
                  />
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* 4. БЛОК «МОИ ТУРНИРЫ» */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <h3 className="text-sm font-extrabold uppercase tracking-wider text-[#8fa89b] flex items-center gap-2">
            <Calendar className="w-4 h-4 text-[#c39a44]" />
            Мои турниры
          </h3>

          <div className="flex items-center gap-1 bg-black/40 p-1 rounded-xl border border-white/10">
            <button
              type="button"
              onClick={() => {
                triggerHaptic('light');
                setTournamentsTab('upcoming');
              }}
              className={`px-3 py-1 rounded-lg text-xs font-bold transition-all ${
                tournamentsTab === 'upcoming'
                  ? 'bg-[#c39a44] text-black shadow-md'
                  : 'text-[#8fa89b] hover:text-white'
              }`}
            >
              Активные ({upcomingTournaments.length})
            </button>
            <button
              type="button"
              onClick={() => {
                triggerHaptic('light');
                setTournamentsTab('history');
              }}
              className={`px-3 py-1 rounded-lg text-xs font-bold transition-all ${
                tournamentsTab === 'history'
                  ? 'bg-[#c39a44] text-black shadow-md'
                  : 'text-[#8fa89b] hover:text-white'
              }`}
            >
              История ({finishedTournaments.length})
            </button>
          </div>
        </div>

        {tournamentsTab === 'upcoming' ? (
          upcomingTournaments.length === 0 ? (
            <div className="p-6 rounded-3xl bg-black/40 border border-white/5 text-center text-xs text-[#8fa89b]">
              У вас пока нет активных записей на турниры. Перейдите во вкладку «Расписание», чтобы занять место за столом!
            </div>
          ) : (
            <div className="space-y-3">
              {upcomingTournaments.map((t) => (
                <div
                  key={t.id}
                  onClick={() => {
                    triggerHaptic('light');
                    openDetail(t.id);
                  }}
                  className="p-4 rounded-3xl bg-black/50 border border-white/10 hover:border-[#c39a44]/40 shadow-xl space-y-2.5 cursor-pointer active:scale-[0.99] transition-all"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-[10px] font-bold text-[#c39a44] uppercase tracking-wider flex items-center gap-1">
                      <Zap className="w-3 h-3" />
                      Вы записаны
                    </span>
                    <span className="text-xs font-semibold text-[#8fa89b] flex items-center gap-1.5">
                      <Clock className="w-3.5 h-3.5" />
                      <span>
                        {new Date(t.startTime).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' })} · {new Date(t.startTime).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}
                      </span>
                    </span>
                  </div>

                  <h4 className="text-base font-extrabold text-white">
                    {t.title}
                  </h4>

                  <div className="flex flex-wrap gap-2 text-xs">
                    <span className="px-2.5 py-0.5 rounded-lg bg-black/60 border border-white/10 font-semibold text-[#d1e0d7]">
                      {t.format || 'NL Holdem'}
                    </span>
                    <span className="px-2.5 py-0.5 rounded-lg bg-black/60 border border-white/10 font-semibold text-[#d1e0d7]">
                      {formatChips(t.startingChips || 10000)} фишек
                    </span>
                    <span className="px-2.5 py-0.5 rounded-lg bg-black/60 border border-white/10 font-semibold text-[#c39a44]">
                      {formatCurrency(t.buyIn)}
                    </span>
                  </div>

                  <div className="text-[11px] text-[#8fa89b] flex items-center gap-1.5 pt-1 border-t border-white/5">
                    <MapPin className="w-3 h-3 text-[#c39a44] shrink-0" />
                    <span className="truncate">{t.clubAddress || 'Монастырская улица, 59, Пермь'}</span>
                  </div>
                </div>
              ))}
            </div>
          )
        ) : finishedTournaments.length === 0 ? (
          <div className="p-6 rounded-3xl bg-black/40 border border-white/5 text-center text-xs text-[#8fa89b]">
            История завершенных турниров пуста. Сыграйте первый турнир, чтобы войти в историю Monte Carlo!
          </div>
        ) : (
          <div className="space-y-2.5">
            {finishedTournaments.map((t) => (
              <div
                key={t.id}
                onClick={() => {
                  triggerHaptic('light');
                  openDetail(t.id);
                }}
                className="p-3.5 px-4 rounded-2xl bg-black/40 border border-white/10 flex items-center justify-between shadow-md cursor-pointer hover:border-white/20 transition-all"
              >
                <div>
                  <div className="text-[10px] text-[#8fa89b] mb-0.5">
                    {new Date(t.startTime).toLocaleDateString('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' })}
                  </div>
                  <div className="text-xs font-bold text-white truncate max-w-[200px]">
                    {t.title}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-extrabold text-[#c39a44]">
                    Завершен
                  </span>
                  <ChevronRight className="w-4 h-4 text-white/40" />
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* 5. ВЫХОД ИЗ ПРОФИЛЯ / СМЕНА АККАУНТА */}
      <div className="pt-2">
        <button
          type="button"
          onClick={() => {
            logout();
          }}
          className="w-full py-3.5 px-4 rounded-2xl bg-red-950/40 hover:bg-red-950/70 active:bg-red-900/50 border border-red-500/30 hover:border-red-500/50 text-red-300 hover:text-red-200 font-bold text-sm flex items-center justify-center gap-2.5 shadow-lg shadow-black/40 transition-all active:scale-[0.98]"
          title="Выйти из аккаунта / Сменить профиль"
          aria-label="Выйти из аккаунта"
        >
          <LogOut className="w-4 h-4 text-red-400 shrink-0" />
          <span>Выйти из профиля</span>
        </button>
        <p className="text-[11px] text-center text-[#8fa89b]/60 mt-2">
          Сбросить сессию и сменить профиль
        </p>
      </div>
    </div>
  );
};
