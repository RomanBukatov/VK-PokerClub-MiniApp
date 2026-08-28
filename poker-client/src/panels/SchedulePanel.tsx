import React, { useEffect } from 'react';
import { useTournamentsStore } from '../store/useTournamentsStore';
import { useUserStore } from '../store/useUserStore';
import { formatCurrency, formatChips } from '../utils/formatters';
import { triggerHaptic } from '../utils/vkBridge';
import { TournamentStatus } from '../types';
import type { Tournament } from '../types';

export const SchedulePanel: React.FC = () => {
  const { tournaments, isLoading, fetchSchedule, openDetail } = useTournamentsStore();
  const { selectedCityId, selectedClubId } = useUserStore();

  useEffect(() => {
    fetchSchedule(selectedCityId, selectedClubId);
  }, [selectedCityId, selectedClubId, fetchSchedule]);

  const handleCardClick = (id: number) => {
    triggerHaptic('light');
    openDetail(id);
  };

  const getSeatsInfo = (t: Tournament) => {
    const total = t.maxSeats || 30;
    const occupied = t.registeredCount || 0;
    const remaining = total - occupied;
    const percentage = Math.min(100, Math.round((occupied / total) * 100));

    if (remaining <= 0) {
      return { text: 'Мест не осталось', percentage: 100, isFull: true };
    }
    if (remaining <= 5) {
      return { text: 'Осталось мало мест', percentage, isFull: false };
    }
    return { text: 'Есть места', percentage, isFull: false };
  };

  const formatCardDate = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      const time = date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
      const now = new Date();
      const isToday = date.toDateString() === now.toDateString();
      if (isToday) return `СЕГОДНЯ · ${time}`;
      
      const day = date.getDate();
      const month = date.toLocaleDateString('ru-RU', { month: 'long' }).toUpperCase();
      return `${day} ${month} · ${time}`;
    } catch {
      return 'СЕГОДНЯ · 19:00';
    }
  };

  const renderCardButton = (t: Tournament, seatsInfo: ReturnType<typeof getSeatsInfo>) => {
    if (t.status === TournamentStatus.Announced) {
      return (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            handleCardClick(t.id);
          }}
          className="w-full py-3 px-4 rounded-full font-bold text-sm transition-all shadow-lg bg-[#192d23] text-[#a4c9b7] border border-[#1e533f] hover:border-[#c39a44] hover:text-white active:scale-[0.98]"
        >
          Подробнее
        </button>
      );
    }

    if (t.status === TournamentStatus.RegistrationOpen) {
      if (t.isUserRegistered) {
        return (
          <button
            type="button"
            onClick={(e) => {
              e.stopPropagation();
              handleCardClick(t.id);
            }}
            className="w-full py-3 px-4 rounded-full font-bold text-sm transition-all shadow-lg bg-[#192d23] text-[#c39a44] border border-[#c39a44]/60 hover:bg-[#1f372b] active:scale-[0.98] flex items-center justify-center gap-2"
          >
            <span className="w-2 h-2 rounded-full bg-[#c39a44]" />
            Вы записаны
          </button>
        );
      }

      if (seatsInfo.isFull) {
        return (
          <button
            type="button"
            disabled
            className="w-full py-3 px-4 rounded-full font-bold text-sm shadow-lg bg-neutral-800/80 text-neutral-400 border border-neutral-700/50 cursor-not-allowed"
          >
            Мест нет
          </button>
        );
      }

      return (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            handleCardClick(t.id);
          }}
          className="w-full py-3 px-4 rounded-full font-bold text-sm transition-all shadow-lg bg-[#c39a44] text-white hover:brightness-105 active:scale-[0.98]"
        >
          Зарегистрироваться
        </button>
      );
    }

    if (t.status === TournamentStatus.Running) {
      return (
        <button
          type="button"
          disabled
          className="w-full py-3 px-4 rounded-full font-bold text-sm shadow-lg bg-neutral-800/80 text-neutral-400 border border-neutral-700/50 cursor-not-allowed"
        >
          Идет турнир
        </button>
      );
    }

    if (t.status === TournamentStatus.Finished) {
      return (
        <button
          type="button"
          disabled
          className="w-full py-3 px-4 rounded-full font-bold text-sm shadow-lg bg-neutral-800/80 text-neutral-400 border border-neutral-700/50 cursor-not-allowed"
        >
          Турнир завершен
        </button>
      );
    }

    return (
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          handleCardClick(t.id);
        }}
        className="w-full py-3 px-4 rounded-full font-bold text-sm transition-all shadow-lg bg-[#192d23] text-white border border-white/10"
      >
        Подробнее
      </button>
    );
  };

  return (
    <div className="px-5 pb-24 animate-fade-in space-y-4">
      {isLoading ? (
        <div className="py-16 text-center text-xs text-[#8fa89b] animate-pulse">
          Загрузка расписания турниров...
        </div>
      ) : tournaments.length === 0 ? (
        <div className="py-16 text-center text-xs text-[#8fa89b] bg-black/40 rounded-3xl p-6 border border-white/5">
          В расписании пока нет запланированных турниров.
        </div>
      ) : (
        tournaments.map((t) => {
          const seatsInfo = getSeatsInfo(t);

          return (
            <div
              key={t.id}
              onClick={() => handleCardClick(t.id)}
              className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl shadow-black/40 active:scale-[0.99] transition-all cursor-pointer"
            >
              {/* Дата и статус */}
              <div className="flex items-center justify-between mb-2">
                <div className="text-[11px] font-bold text-[#d1e0d7] uppercase tracking-wider">
                  {formatCardDate(t.startTime)}
                </div>
                {t.status === TournamentStatus.Announced && (
                  <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-blue-950/70 border border-blue-500/40 text-blue-300">
                    Анонс
                  </span>
                )}
                {t.status === TournamentStatus.RegistrationOpen && t.isUserRegistered && (
                  <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-[#192d23] border border-[#c39a44]/60 text-[#c39a44]">
                    Вы записаны
                  </span>
                )}
                {t.status === TournamentStatus.Running && (
                  <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-emerald-950/70 border border-emerald-500/40 text-emerald-300">
                    Идет турнир
                  </span>
                )}
                {t.status === TournamentStatus.Finished && (
                  <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider bg-neutral-900 border border-neutral-700 text-neutral-400">
                    Завершен
                  </span>
                )}
              </div>

              {/* Название турнира */}
              <h2 className="text-xl font-extrabold text-white mb-3">
                {t.title}
              </h2>

              {/* Теги характеристик */}
              <div className="flex flex-wrap gap-2 mb-4">
                <span className="px-3.5 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                  {t.format || 'no limit'}
                </span>
                <span className="px-3.5 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                  стартовый стек {formatChips(t.startingChips || 10000)}
                </span>
                <span className="px-3.5 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                  {formatCurrency(t.buyIn)}
                </span>
              </div>

              {/* Блок мест */}
              <div className="mb-4">
                <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1.5">
                  МЕСТА
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-bold text-white whitespace-nowrap">
                    {seatsInfo.text} ({t.registeredCount || 0} / {t.maxSeats || 30})
                  </span>
                  <div className="flex-1 h-2 rounded-full bg-[#132c20] overflow-hidden">
                    <div
                      className="h-full rounded-full bg-[#3d6550] transition-all duration-300"
                      style={{ width: `${Math.max(10, seatsInfo.percentage)}%` }}
                    />
                  </div>
                </div>
              </div>

              {/* Кнопка регистрации */}
              {renderCardButton(t, seatsInfo)}
            </div>
          );
        })
      )}
    </div>
  );
};
