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

  const getStatusBadge = (t: Tournament) => {
    if (t.status === TournamentStatus.Finished) {
      return (
        <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-[#142820] text-[#c39a44] border border-[#c39a44]/40">
          🏆 Завершен · Очки начислены
        </span>
      );
    }
    if (t.status === TournamentStatus.RegistrationOpen) {
      return (
        <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-[#0d2a1c] text-[#34d399] border border-[#34d399]/30">
          🟢 Идет запись ({t.registeredCount}/{t.maxSeats})
        </span>
      );
    }
    if (t.status === TournamentStatus.Announced) {
      return (
        <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-blue-950/50 text-blue-300 border border-blue-400/30">
          🔵 Анонс ({t.registeredCount} в списке)
        </span>
      );
    }
    return null;
  };

  return (
    <div className="px-5 pb-24 animate-fade-in space-y-4">
      <div className="text-[11px] text-[#8fa89b] px-1 font-medium">
        📋 Список турниров клуба. Нажмите на событие для просмотра участников на ресепшене или начисления/корректировки очков:
      </div>

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
              className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl shadow-black/40 active:scale-[0.99] transition-all cursor-pointer space-y-2.5 hover:border-white/20"
            >
              {/* Верхняя строка: Дата, статус и шеврон */}
              <div className="flex items-center justify-between gap-2">
                <span className="text-[11px] font-bold text-[#d1e0d7] uppercase tracking-wider">
                  {formatCardDate(t.startTime)}
                </span>

                <div className="flex items-center gap-2">
                  {getStatusBadge(t)}
                  <ChevronRight className="w-4 h-4 text-white/50" />
                </div>
              </div>

              {/* Название турнира */}
              <h2 className="text-lg font-extrabold text-white">
                {t.title}
              </h2>

              {/* Информация о клубе и игроках */}
              <div className="flex items-center justify-between text-xs text-[#8fa89b] pt-0.5">
                <span>{t.clubName || 'Monte Carlo'} · {t.cityName || 'Пермь'}</span>
                <span className="text-[#c39a44] font-semibold">
                  {isFinished ? 'Скорректировать очки →' : 'Участники и начисление →'}
                </span>
              </div>
            </div>
          );
        })
      )}

      {/* Модальное окно начисления очков и участников */}
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
