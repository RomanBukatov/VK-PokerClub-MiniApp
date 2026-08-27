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

  const userRank = currentUserEntry?.rank || 50;
  const userRating = currentUserEntry?.totalRating || 420;

  const getInitials = (first?: string, last?: string) => {
    const f = first?.[0] || 'А';
    const l = last?.[0] || 'К';
    return `${f}${l}`.toUpperCase();
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
            # {userRank}
          </div>
        </div>

        <div className="text-2xl font-black text-white">
          {userRating} очков
        </div>
      </div>

      {/* Заголовки таблицы */}
      <div className="flex items-center justify-between px-3 text-[10px] font-bold text-[#7d9b8c] uppercase tracking-wider">
        <div className="flex items-center gap-6">
          <span className="w-4">МЕСТО</span>
          <span>ИГРОК</span>
        </div>
        <span>ОЧКИ</span>
      </div>

      {/* Список игроков */}
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
          {leaderboard.map((player) => (
            <div
              key={player.id}
              className="p-3 px-4 rounded-2xl bg-black/40 border border-white/10 flex items-center justify-between shadow-md"
            >
              <div className="flex items-center gap-4">
                <span className="w-4 text-sm font-bold text-white text-center">
                  {player.rank}
                </span>

                <div className="w-8 h-8 rounded-full bg-[#606a66] flex items-center justify-center font-bold text-xs text-white overflow-hidden shrink-0">
                  {player.avatarUrl ? (
                    <img src={player.avatarUrl} alt="" className="w-full h-full object-cover" />
                  ) : (
                    getInitials(player.firstName, player.lastName)
                  )}
                </div>

                <span className="text-sm font-semibold text-white">
                  {player.firstName} {player.lastName}
                </span>
              </div>

              <div className="text-sm font-bold text-white">
                {player.totalRating.toLocaleString('ru-RU')}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
