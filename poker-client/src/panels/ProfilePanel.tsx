import React, { useEffect } from 'react';
import { useUserStore } from '../store/useUserStore';
import { useTournamentsStore } from '../store/useTournamentsStore';
import { useRatingsStore } from '../store/useRatingsStore';
import { formatChips, formatCurrency } from '../utils/formatters';
import { triggerHaptic } from '../utils/vkBridge';
import { TournamentStatus } from '../types';

export const ProfilePanel: React.FC = () => {
  const { vkUser } = useUserStore();
  const { myTournaments, fetchMyTournaments, openDetail } = useTournamentsStore();
  const { leaderboard, fetchLeaderboard } = useRatingsStore();

  useEffect(() => {
    fetchMyTournaments();
    fetchLeaderboard();
  }, [fetchMyTournaments, fetchLeaderboard]);

  const userEntry = vkUser 
    ? leaderboard.find((u) => u.vkId === vkUser.id.toString())
    : null;

  const upcomingTournament = myTournaments.find(
    (t) => t.isUserRegistered && t.status !== TournamentStatus.Finished && t.status !== TournamentStatus.Canceled
  );

  const finishedTournaments = myTournaments.filter(
    (t) => t.status === TournamentStatus.Finished
  );

  const getInitials = (first?: string, last?: string) => {
    const f = first?.[0] || 'И';
    const l = last?.[0] || 'П';
    return `${f}${l}`.toUpperCase();
  };

  const formatHistoryDate = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });
    } catch {
      return 'Недавно';
    }
  };

  return (
    <div className="px-5 pb-24 animate-fade-in space-y-5">
      {/* Пользователь: Аватар и имя */}
      <div className="flex items-center gap-3.5 pt-1">
        <div className="w-14 h-14 rounded-full bg-[#606a66] flex items-center justify-center font-extrabold text-base text-white overflow-hidden shadow-lg shrink-0">
          {vkUser?.photo_200 ? (
            <img src={vkUser.photo_200} alt="" className="w-full h-full object-cover" />
          ) : (
            getInitials(vkUser?.first_name, vkUser?.last_name)
          )}
        </div>

        <div>
          <h2 className="text-lg font-extrabold text-white">
            {vkUser?.first_name} {vkUser?.last_name}
          </h2>
          <p className="text-xs text-[#8fa89b] mt-0.5">
            {myTournaments.length} {myTournaments.length === 1 ? 'турнир' : myTournaments.length < 5 ? 'турнира' : 'турниров'}
          </p>
        </div>
      </div>

      {/* Карточка текущего сезона */}
      <div className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl flex items-center justify-between">
        <div>
          <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1">
            ТЕКУЩИЙ СЕЗОН
          </div>
          <div className="text-xl font-extrabold text-white">
            {userEntry ? `# ${userEntry.rank}` : 'Не в рейтинге'}
          </div>
        </div>

        <div className="text-2xl font-black text-white">
          {userEntry ? `${userEntry.totalRating.toLocaleString('ru-RU')} очков` : '0 очков'}
        </div>
      </div>

      {/* Ближайший турнир */}
      {upcomingTournament && (
        <div>
          <h3 className="text-lg font-extrabold text-white mb-3">
            Ближайший турнир
          </h3>

          <div
            onClick={() => {
              triggerHaptic('light');
              openDetail(upcomingTournament.id);
            }}
            className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl space-y-3 cursor-pointer active:scale-[0.99] transition-all"
          >
            <div>
              <div className="text-[10px] uppercase font-bold text-[#8fa89b]">ТУРНИР</div>
              <div className="text-base font-bold text-white mt-0.5">{upcomingTournament.title}</div>
            </div>

            <div className="flex flex-wrap gap-2">
              <span className="px-3 py-0.5 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                {upcomingTournament.format || 'NL Holdem'}
              </span>
              <span className="px-3 py-0.5 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                стартовый стек {formatChips(upcomingTournament.startingChips || 10000)}
              </span>
              <span className="px-3 py-0.5 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                {formatCurrency(upcomingTournament.buyIn)}
              </span>
            </div>

            <div>
              <div className="text-[10px] uppercase font-bold text-[#8fa89b]">АДРЕС</div>
              <div className="text-xs font-semibold text-white mt-0.5">
                {upcomingTournament.clubAddress || 'Монастырская улица, 59, Пермь'}
              </div>
            </div>

            <div>
              <div className="text-[10px] uppercase font-bold text-[#8fa89b]">ВРЕМЯ</div>
              <div className="text-sm font-bold text-white mt-0.5">
                19:00
              </div>
            </div>
          </div>
        </div>
      )}

      {/* История турниров */}
      <div>
        <h3 className="text-lg font-extrabold text-white mb-3">
          История
        </h3>

        {finishedTournaments.length === 0 ? (
          <div className="p-5 rounded-3xl bg-black/40 border border-white/5 text-center text-xs text-[#8fa89b]">
            Сыграйте первый турнир, чтобы набрать очки и войти в историю клуба!
          </div>
        ) : (
          <div className="space-y-3">
            {finishedTournaments.map((t) => (
              <div
                key={t.id}
                onClick={() => {
                  triggerHaptic('light');
                  openDetail(t.id);
                }}
                className="p-4 px-5 rounded-3xl bg-black/40 border border-white/10 flex items-center justify-between shadow-md cursor-pointer hover:border-white/20 transition-all"
              >
                <div>
                  <div className="text-[10px] text-[#8fa89b] mb-0.5">
                    {formatHistoryDate(t.startTime)}
                  </div>
                  <div className="text-sm font-bold text-white">{t.title}</div>
                </div>
                <div className="text-sm font-extrabold text-[#c39a44]">
                  Завершен
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
