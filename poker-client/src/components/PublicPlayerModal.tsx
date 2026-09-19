import React, { useEffect, useState } from 'react';
import { 
  X, 
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
  ShieldAlert, 
  CreditCard 
} from 'lucide-react';
import { usersApi } from '../api/usersApi';
import type { PublicUserProfile, Achievement } from '../types';
import { getRankProgress } from '../config/ranks.config';
import { triggerHaptic } from '../utils/vkBridge';

interface PublicPlayerModalProps {
  playerId: number | string;
  onClose: () => void;
  initialData?: Partial<PublicUserProfile>;
}

export const PublicPlayerModal: React.FC<PublicPlayerModalProps> = ({ playerId, onClose }) => {
  const [player, setPlayer] = useState<PublicUserProfile | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;

    const loadProfile = async () => {
      try {
        const data = await usersApi.getPublicProfile(playerId);
        if (isMounted) {
          setPlayer(data);
          setError(null);
        }
      } catch (err) {
        console.error('Ошибка загрузки профиля игрока:', err);
        if (isMounted) {
          setError('Не удалось загрузить досье игрока. Возможно, профиль еще не создан.');
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    };

    loadProfile();

    return () => {
      isMounted = false;
    };
  }, [playerId]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const handleClose = () => {
    triggerHaptic('light');
    onClose();
  };

  const getInitials = (first?: string, last?: string) => {
    const f = first?.[0] || 'И';
    const l = last?.[0] || 'Г';
    return `${f}${l}`.toUpperCase();
  };

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

  const rankProgress = getRankProgress(player?.totalRating ?? 0);

  const tournamentsPlayed = player?.tournamentsPlayed ?? 0;
  const winsCount = player?.winsCount ?? 0;
  const top3Count = player?.top3Count ?? 0;
  const knockoutsCount = player?.knockoutsCount ?? 0;
  const currentRating = player?.totalRating ?? 0;
  const avgPlace = player?.avgPlace && player.avgPlace > 0 ? player.avgPlace.toFixed(2) : '-';
  const winRate = tournamentsPlayed > 0 
    ? `${((winsCount / tournamentsPlayed) * 100).toFixed(1)}%` 
    : '0.0%';

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
  const displayNickname = player?.nickname || (player?.firstName ? `${player.firstName} ${player.lastName || ''}`.trim() : 'Игрок Monte Carlo');
  const displayRealName = player?.nickname && (player.firstName || player.lastName)
    ? `${player.firstName || ''} ${player.lastName || ''}`.trim()
    : null;

  return (
    <div 
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-0 sm:p-4 bg-black/80 backdrop-blur-sm animate-fade-in"
      onClick={handleClose}
    >
      <div 
        className="w-full max-w-md bg-gradient-to-b from-[#0e2a20] via-[#091f17] to-[#04120e] border-t sm:border border-[#c39a44]/30 rounded-t-3xl sm:rounded-3xl p-5 shadow-2xl safe-bottom max-h-[90vh] overflow-y-auto space-y-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Верхняя панель с кнопкой закрытия */}
        <div className="flex items-center justify-between pb-3 border-b border-white/10">
          <div className="flex items-center gap-2">
            <span className="text-sm font-extrabold uppercase tracking-wider text-[#d8af56]">
              Досье оппонента
            </span>
            <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-[#c39a44]/20 border border-[#c39a44]/40 text-[#ffd700]">
              Monte Carlo
            </span>
          </div>

          <button
            type="button"
            onClick={handleClose}
            aria-label="Закрыть"
            className="p-2 rounded-2xl bg-white/5 hover:bg-white/10 border border-white/10 text-[#c39a44] hover:text-white transition-all active:scale-95 shadow-md cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {loading ? (
          <div className="py-16 text-center space-y-3 animate-pulse">
            <div className="w-16 h-16 rounded-2xl bg-white/10 mx-auto" />
            <div className="h-5 w-40 bg-white/10 rounded-lg mx-auto" />
            <div className="h-4 w-28 bg-white/10 rounded-lg mx-auto" />
            <p className="text-xs text-[#8fa89b]">Загрузка данных соперника...</p>
          </div>
        ) : error ? (
          <div className="py-12 text-center space-y-4">
            <div className="w-12 h-12 rounded-full bg-rose-950/60 border border-rose-500/40 flex items-center justify-center mx-auto text-rose-300">
              <ShieldAlert className="w-6 h-6" />
            </div>
            <p className="text-sm font-semibold text-rose-200">{error}</p>
            <button
              type="button"
              onClick={handleClose}
              className="px-5 py-2.5 rounded-full bg-[#1b4d3e] text-white font-bold text-xs hover:bg-[#256a55]"
            >
              Закрыть
            </button>
          </div>
        ) : player ? (
          <>
            {/* 1. АВАТАР, НИКНЕЙМ/ИМЯ, КАРТА */}
            <div className="p-4 rounded-3xl bg-black/40 border border-white/10 shadow-xl relative overflow-hidden">
              <div className="absolute -top-6 -right-6 w-28 h-28 bg-[#c39a44]/15 rounded-full blur-2xl pointer-events-none" />

              <div className="flex items-center gap-4 relative z-10">
                {/* Аватар с бейджем ранга */}
                <div className="relative shrink-0">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-[#2a4d3d] to-[#122e23] border-2 border-[#c39a44]/50 flex items-center justify-center font-black text-xl text-[#d8af56] overflow-hidden shadow-lg shadow-black/60">
                    {player.avatarUrl ? (
                      <img src={player.avatarUrl} alt="" className="w-full h-full object-cover" />
                    ) : (
                      getInitials(player.firstName, player.lastName)
                    )}
                  </div>
                  <div className={`absolute -bottom-1.5 -right-1.5 px-2 py-0.5 rounded-full text-[9px] font-black uppercase tracking-wider bg-gradient-to-r ${rankProgress.currentRank.gradient} text-white shadow-md border ${rankProgress.currentRank.borderColor} flex items-center gap-1`}>
                    <span>{rankProgress.currentRank.icon}</span>
                    <span>{rankProgress.badgeText}</span>
                  </div>
                </div>

                {/* Информация об игроке */}
                <div className="min-w-0 flex-1">
                  <h3 className="text-lg font-black text-white truncate">
                    {displayNickname}
                  </h3>
                  {displayRealName && (
                    <p className="text-xs text-[#a4c9b7] truncate mt-0.5 font-medium">
                      {displayRealName}
                    </p>
                  )}

                  <div className="flex items-center gap-2 mt-2">
                    {player.clubCardId ? (
                      <span className="px-2.5 py-0.5 rounded-lg bg-black/60 border border-[#c39a44]/40 text-[10px] font-extrabold text-[#ffd700] flex items-center gap-1">
                        <CreditCard className="w-3 h-3 text-[#c39a44]" />
                        #{player.clubCardId}
                      </span>
                    ) : (
                      <span className="px-2 py-0.5 rounded-lg bg-white/5 border border-white/10 text-[10px] text-[#8fa89b]">
                        Карта не привязана
                      </span>
                    )}

                    <span className="text-[11px] font-extrabold text-[#d8af56]">
                      {currentRating.toLocaleString('ru-RU')} RPS
                    </span>
                  </div>
                </div>
              </div>
            </div>

            {/* 2. КАРТОЧКА РАНГА (1-15 LVL) С ПРОГРЕСС-БАРОМ */}
            <div className="p-4 rounded-3xl bg-gradient-to-b from-[#0e2a20] to-[#071912] border border-[#c39a44]/30 shadow-xl space-y-2.5 relative overflow-hidden">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <span className="text-2xl shrink-0 drop-shadow">{rankProgress.currentRank.icon}</span>
                  <div>
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-black text-white tracking-tight">
                        {rankProgress.displayName}
                      </span>
                      <span className={`px-2 py-0.5 rounded-md text-[9px] font-extrabold uppercase tracking-wide ${rankProgress.currentRank.badgeBg} ${rankProgress.currentRank.textColor} border ${rankProgress.currentRank.borderColor}`}>
                        {rankProgress.badgeText}
                      </span>
                    </div>
                    <p className="text-[10px] text-[#8fa89b]">
                      Уровень {rankProgress.currentRank.level} из 15
                    </p>
                  </div>
                </div>

                <div className="text-right">
                  <span className="text-xs font-black text-[#ffd700]">
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

              {/* Описание перехода к следующему уровню */}
              <div className="flex items-center justify-between text-[10px] font-bold text-[#d1e0d7] pt-0.5">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <span className="text-[#8fa89b]">До ранга</span>
                  <span className="text-[#ffd700] font-extrabold">{rankProgress.nextRank.name}</span>
                  <span className="text-[#8fa89b]">осталось</span>
                  <span className="text-[#ffd700] font-black">{rankProgress.pointsToNext.toLocaleString('ru-RU')}</span>
                  <span className="text-[#8fa89b]">очков</span>
                </div>

                <div className="text-[10px] text-[#8fa89b] shrink-0 ml-1">
                  {currentRating.toLocaleString('ru-RU')} / {rankProgress.targetPoints.toLocaleString('ru-RU')}
                </div>
              </div>
            </div>

            {/* 3. СЕТКА МЕТРИК (AVG позиция, ТОП-3, Win Rate %, Победы, Нокауты) */}
            <div className="space-y-2">
              <h4 className="text-xs font-extrabold uppercase tracking-wider text-[#8fa89b] flex items-center gap-2">
                <Award className="w-4 h-4 text-[#c39a44]" />
                Метрики игрока
              </h4>

              <div className="grid grid-cols-2 gap-2.5">
                {/* AVG Позиция */}
                <div className="p-3.5 rounded-2xl bg-black/40 border border-white/10 shadow-md">
                  <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
                    AVG Позиция
                  </div>
                  <div className="text-xl font-black text-white mt-0.5">
                    {avgPlace}
                  </div>
                  <div className="text-[9px] text-[#7d9b8c] mt-0.5">
                    Среднее место за столом
                  </div>
                </div>

                {/* ТОП-3 */}
                <div className="p-3.5 rounded-2xl bg-black/40 border border-white/10 shadow-md">
                  <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
                    ТОП-3
                  </div>
                  <div className="text-xl font-black text-[#ffd700] mt-0.5">
                    {top3Count}
                  </div>
                  <div className="text-[9px] text-[#7d9b8c] mt-0.5">
                    Призовые финиши
                  </div>
                </div>

                {/* Win Rate % */}
                <div className="p-3.5 rounded-2xl bg-black/40 border border-white/10 shadow-md">
                  <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
                    Win Rate %
                  </div>
                  <div className="text-xl font-black text-emerald-400 mt-0.5">
                    {winRate}
                  </div>
                  <div className="text-[9px] text-[#7d9b8c] mt-0.5">
                    Процент первых мест
                  </div>
                </div>

                {/* Победы */}
                <div className="p-3.5 rounded-2xl bg-black/40 border border-white/10 shadow-md">
                  <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
                    Победы
                  </div>
                  <div className="text-xl font-black text-white mt-0.5">
                    {winsCount}
                  </div>
                  <div className="text-[9px] text-[#7d9b8c] mt-0.5">
                    1-е места ({tournamentsPlayed} туриков)
                  </div>
                </div>

                {/* Нокауты */}
                <div className="col-span-2 p-3.5 rounded-2xl bg-black/40 border border-white/10 shadow-md flex items-center justify-between">
                  <div>
                    <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider">
                      Нокауты
                    </div>
                    <div className="text-xl font-black text-[#d8af56] mt-0.5">
                      {knockoutsCount}
                    </div>
                    <div className="text-[9px] text-[#7d9b8c]">
                      Выбито оппонентов
                    </div>
                  </div>
                  <div className="w-10 h-10 rounded-2xl bg-[#c39a44]/15 border border-[#c39a44]/30 flex items-center justify-center text-[#ffd700]">
                    <Crosshair className="w-5 h-5" />
                  </div>
                </div>
              </div>
            </div>

            {/* 4. БЛОК «ДОСТИЖЕНИЯ ИГРОКА» (7 АЧИВОК) */}
            <div className="space-y-2 pt-1">
              <div className="flex items-center justify-between">
                <h4 className="text-xs font-extrabold uppercase tracking-wider text-[#8fa89b] flex items-center gap-2">
                  <Trophy className="w-4 h-4 text-[#c39a44]" />
                  Достижения игрока
                </h4>
                <span className="text-[11px] font-bold text-[#c39a44] px-2.5 py-0.5 rounded-full bg-[#c39a44]/15 border border-[#c39a44]/30">
                  {unlockedCount} из {achievements.length}
                </span>
              </div>

              <div className="space-y-2">
                {achievements.map((ach) => (
                  <div
                    key={ach.id}
                    className={`p-3 rounded-2xl border transition-all flex items-center gap-3 shadow-md ${
                      ach.isUnlocked
                        ? 'bg-gradient-to-r from-[#0a231b] to-[#113527] border-[#c39a44]/50 shadow-[#c39a44]/10'
                        : 'bg-[#0a231b]/90 border-white/10'
                    }`}
                  >
                    {/* Значок ачивки */}
                    <div
                      className={`w-10 h-10 rounded-2xl flex items-center justify-center shrink-0 shadow-inner ${
                        ach.isUnlocked
                          ? 'bg-gradient-to-br from-[#d8af56]/30 to-[#b38833]/20 border border-[#c39a44]/60 shadow-[0_0_10px_rgba(195,154,68,0.25)]'
                          : 'bg-black/50 border border-white/10'
                      }`}
                    >
                      {renderAchievementIcon(ach.id, ach.isUnlocked)}
                    </div>

                    {/* Название, описание и прогресс */}
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center justify-between">
                        <h5 className="text-xs font-black text-white truncate">
                          {ach.title}
                        </h5>
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

                      <p className="text-[10px] text-[#a4c9b7] truncate mt-0.5 font-normal">
                        {ach.description}
                      </p>

                      {/* Полоска прогресса */}
                      <div className="w-full bg-white/10 rounded-full h-1.5 mt-1.5 overflow-hidden">
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
          </>
        ) : null}
      </div>
    </div>
  );
};
