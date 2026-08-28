import React, { useEffect } from 'react';
import { useRatingsStore } from '../store/useRatingsStore';
import { useUserStore } from '../store/useUserStore';
import { triggerHaptic } from '../utils/vkBridge';

export const LeaderboardPanel: React.FC = () => {
  const { leaderboard, isLoading, fetchLeaderboard, seasonTab, setSeasonTab } = useRatingsStore();
  const { vkUser } = useUserStore();

  useEffect(() => {
    fetchLeaderboard();
  }, [fetchLeaderboard]);

  const currentUserEntry = vkUser 
    ? leaderboard.find((u) => u.vkId === vkUser.id.toString())
    : null;

  const getInitials = (first?: string, last?: string) => {
    const f = first?.[0] || 'А';
    const l = last?.[0] || 'К';
    return `${f}${l}`.toUpperCase();
  };

  const getZoneStyle = (rank: number) => {
    if (rank <= 10) {
      return {
        cardBg: 'bg-emerald-950/30 border-emerald-500/40',
        rankColor: 'text-emerald-400 font-extrabold',
        badge: 'Финал',
        badgeColor: 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40',
        avatarBg: 'bg-emerald-800 text-white',
      };
    }
    if (rank <= 15) {
      return {
        cardBg: 'bg-amber-950/25 border-amber-500/30',
        rankColor: 'text-amber-400 font-bold',
        badge: 'Топ-15',
        badgeColor: 'bg-amber-500/20 text-amber-300 border border-amber-500/30',
        avatarBg: 'bg-amber-800/80 text-white',
      };
    }
    if (rank <= 20) {
      return {
        cardBg: 'bg-rose-950/20 border-rose-500/30',
        rankColor: 'text-rose-400 font-bold',
        badge: 'Зона риска',
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
          onClick={() => { triggerHaptic('light'); setSeasonTab('all-time'); }}
          className={`px-5 py-2 rounded-full text-xs font-bold transition-all ${
            seasonTab === 'all-time'
              ? 'bg-[#3b4e44] text-white shadow-md'
              : 'bg-black/40 border border-[#1d3b2c] text-[#7d9b8c] hover:text-white'
          }`}
        >
          За все время
        </button>
      </div>

      {/* Карточка текущего сезона пользователя */}
      <div className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl flex items-center justify-between">
        <div>
          <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1">
            ТЕКУЩИЙ СЕЗОН
          </div>
          <div className="text-xl font-extrabold text-white">
            {currentUserEntry ? `# ${currentUserEntry.rank}` : 'Не в рейтинге'}
          </div>
        </div>

        <div className="text-2xl font-black text-white">
          {currentUserEntry ? `${currentUserEntry.totalRating.toLocaleString('ru-RU')} очков` : '0 очков'}
        </div>
      </div>

      {/* Легенда зон рейтинга */}
      <div className="flex items-center justify-between px-2 text-[10px] font-semibold text-[#8fa89b]">
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-emerald-400" />
          <span>1-10 Финал</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-amber-400" />
          <span>11-15 Топ</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-rose-400" />
          <span>16-20 Риск</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="w-2 h-2 rounded-full bg-gray-500" />
          <span>21+</span>
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
      ) : leaderboard.length === 0 ? (
        <div className="py-12 text-center text-xs text-[#8fa89b] bg-black/40 rounded-3xl p-6 border border-white/5">
          Рейтинг еще не сформирован. Сыграйте первый турнир!
        </div>
      ) : (
        <div className="space-y-2.5">
          {leaderboard.map((player) => {
            const zone = getZoneStyle(player.rank);

            return (
              <div
                key={player.id}
                className={`p-3 px-4 rounded-2xl border flex items-center justify-between shadow-md transition-all ${zone.cardBg}`}
              >
                <div className="flex items-center gap-3.5">
                  <span className={`w-5 text-sm text-center ${zone.rankColor}`}>
                    {player.rank}
                  </span>

                  <div className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs overflow-hidden shrink-0 ${zone.avatarBg}`}>
                    {player.avatarUrl ? (
                      <img src={player.avatarUrl} alt="" className="w-full h-full object-cover" />
                    ) : (
                      getInitials(player.firstName, player.lastName)
                    )}
                  </div>

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
                  {player.totalRating.toLocaleString('ru-RU')}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
