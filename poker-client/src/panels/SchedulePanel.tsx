import React, { useEffect } from 'react';
import { useTournamentsStore } from '../store/useTournamentsStore';
import { useUserStore } from '../store/useUserStore';
import { formatCurrency, formatChips } from '../utils/formatters';
import { triggerHaptic } from '../utils/vkBridge';
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
              {/* Дата и время */}
              <div className="text-[11px] font-bold text-[#d1e0d7] uppercase tracking-wider mb-2">
                {formatCardDate(t.startTime)}
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
                    {seatsInfo.text}
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
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handleCardClick(t.id);
                }}
                className={`w-full py-3 px-4 rounded-full font-bold text-sm transition-all shadow-lg ${
                  t.isUserRegistered
                    ? 'bg-[#192d23] text-[#c39a44] border border-[#c39a44]/50'
                    : seatsInfo.isFull
                    ? 'bg-neutral-800 text-neutral-400'
                    : 'bg-[#c39a44] text-white hover:brightness-105 active:scale-[0.98]'
                }`}
              >
                {t.isUserRegistered ? 'Место подтверждено' : seatsInfo.isFull ? 'Мест нет' : 'Зарегистрироваться'}
              </button>
            </div>
          );
        })
      )}
    </div>
  );
};
