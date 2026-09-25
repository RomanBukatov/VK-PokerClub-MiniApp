import React, { useEffect, useState } from 'react';
import { useRatingsStore } from '../store/useRatingsStore';
import { useTournamentsStore } from '../store/useTournamentsStore';
import { useUserStore } from '../store/useUserStore';
import { triggerHaptic } from '../utils/vkBridge';
import { PublicPlayerModal } from '../components/PublicPlayerModal';
import { PlayerAvatar } from '../components/PlayerAvatar';

// eslint-disable-next-line react-refresh/only-export-components
export const getLeaderboardSubtitle = (seasonTab: string, seasonName?: string) => {
  const isAllTime = seasonTab === 'all' || seasonTab === 'all-time';
  const activeSeasonName = seasonName?.trim() || 'Осень 2026';
  return isAllTime ? 'Общий зачет клуба · Зал славы' : `Сезон: ${activeSeasonName}`;
};

// eslint-disable-next-line react-refresh/only-export-components
export const getZoneStyle = (rank: number) => {
  if (rank <= 70) {
    return {
      cardBg: 'bg-emerald-950/30 border-emerald-500/40',
      rankColor: 'text-emerald-400 font-extrabold',
      badge: 'Финал',
      badgeColor: 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40',
      avatarBg: 'bg-emerald-800 text-white',
    };
  }
  if (rank <= 100) {
    return {
      cardBg: 'bg-amber-950/25 border-amber-500/30',
      rankColor: 'text-amber-400 font-bold',
      badge: 'Претендент',
      badgeColor: 'bg-amber-500/20 text-amber-300 border border-amber-500/30',
      avatarBg: 'bg-amber-800/80 text-white',
    };
  }
  if (rank <= 120) {
    return {
      cardBg: 'bg-rose-950/20 border-rose-500/30',
      rankColor: 'text-rose-400 font-bold',
      badge: 'Риск',
      badgeColor: 'bg-rose-500/20 text-rose-300 border border-rose-500/30',
      avatarBg: 'bg-rose-900/60 text-white',
    };
  }
  return {
    cardBg: 'bg-black/40 border-white/10',
    rankColor: 'text-gray-400 font-medium',
    badge: null,
    badgeColor: '',
    avatarBg: 'bg-[#606a66] text-white',
  };
};

// eslint-disable-next-line react-refresh/only-export-components
export const getDisplayRank = (entry: { rank: number; sheetRank?: number | null }): number =>
  entry.sheetRank ?? entry.rank;

// eslint-disable-next-line react-refresh/only-export-components
export const getUserRankLabel = (
  currentUserRank: number | null,
  userPoints: number,
  leaderboardLength: number
): string => {
  if (currentUserRank != null && userPoints > 0) {
    return `# ${currentUserRank}`;
  }
  if (userPoints > 0) {
    return `${leaderboardLength > 0 ? `${leaderboardLength}+` : '50+'} в клубе`;
  }
  return 'Не в рейтинге';
};

export const LeaderboardPanel: React.FC = () => {
  const { 
    leaderboard, 
    totalCount, 
    isLoading, 
    fetchLeaderboard, 
    loadMore, 
    isLoadingMore, 
    seasonTab, 
    setSeasonTab,
    seasonName,
    ratingsError,
  } = useRatingsStore();
  const { fetchSchedule } = useTournamentsStore();
  const { vkUser, profile } = useUserStore();

  const [selectedPlayerId, setSelectedPlayerId] = useState<number | string | null>(null);

  const isAllTime = seasonTab === 'all' || seasonTab === 'all-time';
  const activeSeasonName = seasonName?.trim() || 'Осень 2026';
  const subtitle = getLeaderboardSubtitle(seasonTab, seasonName);

  useEffect(() => {
    fetchLeaderboard(isAllTime ? 'all' : 'season');
  }, [isAllTime, fetchLeaderboard]);

  useEffect(() => {
    // Фоновое тихое обновление данных каждые 5 минут
    const interval = setInterval(() => {
      fetchLeaderboard(false); // без показа блокирующего лоадера
      fetchSchedule(false);
    }, 5 * 60 * 1000);

    return () => clearInterval(interval);
  }, [fetchLeaderboard, fetchSchedule]);

  const currentUserEntry = vkUser 
    ? leaderboard.find((u) => u.vkId === vkUser.id.toString())
    : null;

  const userPoints = currentUserEntry != null
    ? (currentUserEntry.points ?? 0)
    : (seasonTab === 'current' ? (profile?.seasonRating ?? 0) : (profile?.totalRating ?? 0));

  const currentUserRank = currentUserEntry != null
    ? (seasonTab === 'current' ? currentUserEntry.rank : getDisplayRank(currentUserEntry))
    : (seasonTab === 'all' && (profile?.totalRating ?? 0) > 0 ? (profile?.sheetRank ?? null) : null);

  const userRankLabel = getUserRankLabel(currentUserRank, userPoints, leaderboard.length);

  return (
    <div className="px-5 pb-24 animate-fade-in space-y-4">
      {/* Сезонные табы-пиллы */}
      <div className="flex gap-2">
        <button
          onClick={() => { triggerHaptic('light'); setSeasonTab('current'); }}
          className={`px-5 py-2 rounded-full text-xs font-bold transition-all ${
            seasonTab === 'current'
              ? 'bg-[#3b4e44] text-white shadow-md'
              : 'bg-black/40 border border-[#1d3b2c] text-[#7d9b8c] hover:text-white'
          }`}
        >
          Текущий сезон
        </button>

        <button
          onClick={() => { triggerHaptic('light'); setSeasonTab('all'); }}
          className={`px-5 py-2 rounded-full text-xs font-bold transition-all ${
            isAllTime
              ? 'bg-[#3b4e44] text-white shadow-md'
              : 'bg-black/40 border border-[#1d3b2c] text-[#7d9b8c] hover:text-white'
          }`}
        >
          За все время
        </button>
      </div>

      {/* Подзаголовок для доступности и тестов */}
      <div className="sr-only" data-testid="leaderboard-subtitle">{subtitle}</div>

      {/* Карточка текущего сезона пользователя */}
      <div className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl flex items-center justify-between">
        <div>
          <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1">
            {seasonTab === 'current' ? `ТЕКУЩИЙ СЕЗОН · ${activeSeasonName.toUpperCase()}` : 'ЗА ВСЕ ВРЕМЯ'}
          </div>
          <div className="text-xl font-extrabold text-white">
            {userRankLabel}
          </div>
        </div>

        <div className="text-2xl font-black text-white">
          {userPoints.toLocaleString('ru-RU')} очков
        </div>
      </div>

      {/* Легенда зон рейтинга */}
      <div className="flex items-center justify-between px-2 text-[10px] font-semibold text-[#8fa89b]">
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-emerald-400" />
          <span>1–70 Финал</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-amber-400" />
          <span>71–100 Претендент</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-rose-400" />
          <span>101–120 Риск</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-gray-500" />
          <span>121+</span>
        </div>
      </div>

      {/* Заголовки таблицы */}
      <div className="flex items-center justify-between px-3 text-[10px] font-bold text-[#7d9b8c] uppercase tracking-wider">
        <div className="flex items-center gap-6">
          <span className="w-5 text-center">МЕСТО</span>
          <span>ИГРОК</span>
        </div>
        <span>ОЧКИ</span>
      </div>

      {/* Список игроков с цветовой подсветкой зон */}
      {isLoading ? (
        <div className="py-12 text-center text-xs text-[#8fa89b] animate-pulse">
          Загрузка рейтинга игроков...
        </div>
      ) : ratingsError ? (
        <div className="py-12 text-center text-xs text-red-300 bg-red-950/40 rounded-3xl p-6 border border-red-500/30 space-y-3">
          <div>{ratingsError}</div>
          <button
            type="button"
            onClick={() => {
              triggerHaptic('light');
              fetchLeaderboard(isAllTime ? 'all' : 'season');
            }}
            className="px-4 py-2 rounded-xl bg-[#c39a44] text-black font-bold text-xs hover:bg-[#ffd700] transition-all active:scale-95"
          >
            Повторить
          </button>
        </div>
      ) : leaderboard.length === 0 ? (
        <div className="py-12 text-center text-xs text-[#8fa89b] bg-black/40 rounded-3xl p-6 border border-white/5">
          Рейтинг еще не сформирован. Сыграйте первый турнир!
        </div>
      ) : (
        <div className="space-y-2.5">
          {leaderboard.map((player) => {
            const displayRank = seasonTab === 'current' ? player.rank : getDisplayRank(player);
            const zone = getZoneStyle(displayRank);

            return (
              <div
                key={player.id}
                onClick={() => {
                  triggerHaptic('light');
                  setSelectedPlayerId(player.id);
                }}
                className={`p-3 px-4 rounded-2xl border flex items-center justify-between shadow-md transition-all cursor-pointer hover:border-emerald-500/40 active:scale-[0.99] ${zone.cardBg}`}
              >
                <div className="flex items-center gap-3.5">
                  <span className={`w-5 text-sm text-center ${zone.rankColor}`}>
                    {displayRank}
                  </span>

                  <PlayerAvatar
                    avatarUrl={player.avatarUrl}
                    firstName={player.firstName}
                    lastName={player.lastName}
                    className="w-8 h-8 rounded-full"
                    textClassName="text-[11px] font-black"
                  />

                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold text-white">
                      {player.firstName} {player.lastName}
                    </span>
                    {zone.badge && (
                      <span className={`px-1.5 py-0.5 rounded text-[9px] font-bold ${zone.badgeColor}`}>
                        {zone.badge}
                      </span>
                    )}
                  </div>
                </div>

                <div className="text-sm font-bold text-white">
                  {(player.points ?? (seasonTab === 'current' ? (player.seasonRating ?? player.totalRating) : player.totalRating)).toLocaleString('ru-RU')}
                </div>
              </div>
            );
          })}

          {leaderboard.length < totalCount && (
            <div className="pt-3 pb-2 flex justify-center">
              <button
                type="button"
                onClick={() => {
                  triggerHaptic('light');
                  loadMore();
                }}
                disabled={isLoadingMore}
                className="w-full py-3.5 px-5 rounded-2xl bg-gradient-to-r from-[#d8af56] via-[#ffd700] to-[#b38833] hover:from-[#ffd700] hover:to-[#c39a44] text-[#122e23] font-black text-xs uppercase tracking-wider shadow-lg shadow-[#c39a44]/25 active:scale-[0.98] transition-all flex items-center justify-center gap-2 border border-[#ffd700]/40 disabled:opacity-60 cursor-pointer"
              >
                {isLoadingMore ? (
                  <>
                    <div className="w-4 h-4 border-2 border-[#122e23]/30 border-t-[#122e23] rounded-full animate-spin" />
                    <span>Загрузка игроков...</span>
                  </>
                ) : (
                  <span>Показать еще (загружено {leaderboard.length} из {totalCount})</span>
                )}
              </button>
            </div>
          )}
        </div>
      )}

      {selectedPlayerId !== null && (
        <PublicPlayerModal
          key={selectedPlayerId}
          playerId={selectedPlayerId}
          onClose={() => {
            setSelectedPlayerId(null);
          }}
        />
      )}
    </div>
  );
};
