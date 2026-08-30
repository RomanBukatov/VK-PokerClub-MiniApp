import React, { useEffect, useState } from 'react';
import { ChevronRight } from 'lucide-react';
import { useTournamentsStore } from '../../store/useTournamentsStore';
import { useUserStore } from '../../store/useUserStore';
import { AdminAssignPointsModal } from './AdminAssignPointsModal';
import { triggerHaptic } from '../../utils/vkBridge';
import { TournamentStatus, type Tournament } from '../../types';

export const AdminTournamentsPanel: React.FC = () => {
  const { tournaments, isLoading, fetchAdminSchedule } = useTournamentsStore();
  const { selectedCityId, selectedClubId } = useUserStore();

  const [selectedTournamentForPoints, setSelectedTournamentForPoints] = useState<Tournament | null>(null);

  useEffect(() => {
    fetchAdminSchedule(selectedCityId, selectedClubId);
  }, [selectedCityId, selectedClubId, fetchAdminSchedule]);

  const handleOpenAssign = (t: Tournament) => {
    triggerHaptic('medium');
    setSelectedTournamentForPoints(t);
  };

  const formatCardDate = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      const time = date.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
      const day = date.getDate();
      const month = date.toLocaleDateString('ru-RU', { month: 'long' }).toUpperCase();
      return `${day} ${month} · ${time}`;
    } catch {
      return '18 ИЮЛЯ · 19:00';
    }
  };

  return (
    <div className="px-5 pb-24 animate-fade-in space-y-4">
      {isLoading ? (
        <div className="py-16 text-center text-xs text-[#8fa89b] animate-pulse">
          Загрузка игр для управления...
        </div>
      ) : tournaments.length === 0 ? (
        <div className="py-16 text-center text-xs text-[#8fa89b] bg-black/40 rounded-3xl p-6 border border-white/5">
          Нет доступных турниров для управления.
        </div>
      ) : (
        tournaments.map((t) => {
          const isFinished = t.status === TournamentStatus.Finished;

          return (
            <div
              key={t.id}
              onClick={() => handleOpenAssign(t)}
              className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl shadow-black/40 active:scale-[0.99] transition-all cursor-pointer space-y-2"
            >
              {/* Верхняя строка: Дата, статус и шеврон */}
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-[#d1e0d7] uppercase tracking-wider">
                  {formatCardDate(t.startTime)}
                </span>

                <div className="flex items-center gap-3">
                  {isFinished ? (
                    <span className="text-xs font-bold text-[#46625b]">
                      Начислено
                    </span>
                  ) : (
                    <span className="text-xs font-bold text-[#d72a4b]">
                      Не начислено
                    </span>
                  )}
                  <ChevronRight className="w-5 h-5 text-white/70" />
                </div>
              </div>

              {/* Название турнира */}
              <h2 className="text-xl font-extrabold text-white">
                {t.title}
              </h2>

              {/* Количество игроков */}
              <div className="text-xs text-[#8fa89b]">
                {t.registeredCount || 0} игроков
              </div>
            </div>
          );
        })
      )}

      {/* Модальное окно начисления очков */}
      {selectedTournamentForPoints && (
        <AdminAssignPointsModal
          tournament={selectedTournamentForPoints}
          onClose={() => setSelectedTournamentForPoints(null)}
          onSuccess={() => {
            setSelectedTournamentForPoints(null);
            fetchAdminSchedule(selectedCityId, selectedClubId);
          }}
        />
      )}
    </div>
  );
};
