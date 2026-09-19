import React, { useEffect, useState } from 'react';
import { ChevronRight, RefreshCw, CheckCircle2, AlertCircle } from 'lucide-react';
import { useTournamentsStore } from '../../store/useTournamentsStore';
import { useUserStore } from '../../store/useUserStore';
import { useRatingsStore } from '../../store/useRatingsStore';
import { adminApi } from '../../api/adminApi';
import { AdminAssignPointsModal } from './AdminAssignPointsModal';
import { triggerHaptic } from '../../utils/vkBridge';
import { TournamentStatus, type Tournament } from '../../types';

export const AdminTournamentsPanel: React.FC = () => {
  const { tournaments, isLoading, scheduleError, fetchAdminSchedule } = useTournamentsStore();
  const { selectedCityId, selectedCity, selectedClubId } = useUserStore();

  const [selectedTournamentForPoints, setSelectedTournamentForPoints] = useState<Tournament | null>(null);
  const [isSyncing, setIsSyncing] = useState(false);
  const [syncNotification, setSyncNotification] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  // Когда выбрано «Все города» (selectedCity === null или selectedCityId === null),
  // отображаются ВСЕ турниры без фильтрации по городу
  const activeCityId = selectedCity !== undefined ? selectedCity : selectedCityId;

  useEffect(() => {
    const targetCityId = activeCityId === null ? null : activeCityId;
    const targetClubId = targetCityId === null ? null : selectedClubId;
    fetchAdminSchedule(targetCityId, targetClubId);
  }, [activeCityId, selectedClubId, fetchAdminSchedule]);

  const handleSyncSheets = async () => {
    triggerHaptic('medium');
    setIsSyncing(true);
    setSyncNotification(null);

    try {
      const res = await adminApi.syncSheets();
      triggerHaptic('light');
      const message = res.message && res.totalProcessed === 0 && res.updatedCount === 0 && res.createdCount === 0
        ? res.message
        : `Синхронизация Google Sheets завершена: обработано ${res.totalProcessed}, обновлено ${res.updatedCount}, создано ${res.createdCount}.`;
      setSyncNotification({
        type: 'success',
        message,
      });

      // Обновляем списки и лидерборд
      const targetCityId = activeCityId === null ? null : activeCityId;
      const targetClubId = targetCityId === null ? null : selectedClubId;
      fetchAdminSchedule(targetCityId, targetClubId);
      useRatingsStore.getState().fetchLeaderboard();
    } catch (err: unknown) {
      triggerHaptic('heavy');
      const errorMsg = err && typeof err === 'object' && 'response' in err
        ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
        : 'Ошибка синхронизации с Google Sheets.';
      setSyncNotification({
        type: 'error',
        message: errorMsg || 'Ошибка синхронизации с Google Sheets.',
      });
    } finally {
      setIsSyncing(false);
    }
  };

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
      {/* Кнопка синхронизации Google Sheets */}
      <div className="pt-1">
        <button
          type="button"
          disabled={isSyncing}
          onClick={handleSyncSheets}
          className="w-full py-3.5 px-4 rounded-2xl bg-gradient-to-r from-[#173e2f] to-[#0c241b] hover:from-[#1e4d3b] hover:to-[#113125] border border-[#c39a44]/30 text-white font-extrabold text-xs flex items-center justify-center gap-2.5 shadow-lg active:scale-[0.99] transition-all disabled:opacity-60"
        >
          <RefreshCw className={`w-4 h-4 text-[#c39a44] ${isSyncing ? 'animate-spin' : ''}`} />
          <span>{isSyncing ? 'Синхронизация с таблицей "МК РЕЙТИНГ"...' : 'Синхронизировать Google Sheets'}</span>
        </button>
      </div>

      {/* Уведомление о результатах синхронизации */}
      {syncNotification && (
        <div
          className={`p-4 rounded-2xl border text-xs flex items-start gap-2.5 animate-fade-in shadow-lg ${
            syncNotification.type === 'success'
              ? 'bg-emerald-950/80 border-emerald-500/40 text-emerald-200'
              : 'bg-red-950/80 border-red-500/40 text-red-200'
          }`}
        >
          {syncNotification.type === 'success' ? (
            <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0 mt-0.5" />
          ) : (
            <AlertCircle className="w-4 h-4 text-red-400 shrink-0 mt-0.5" />
          )}
          <span className="flex-1 leading-relaxed">{syncNotification.message}</span>
        </div>
      )}

      {scheduleError && (
        <div className="p-4 rounded-2xl bg-red-950/80 border border-red-500/40 text-red-300 text-xs flex items-center justify-between gap-3 shadow-lg animate-fade-in">
          <div className="flex-1 font-medium">{scheduleError}</div>
          <button
            type="button"
            onClick={() => {
              const targetCityId = activeCityId === null ? null : activeCityId;
              const targetClubId = targetCityId === null ? null : selectedClubId;
              fetchAdminSchedule(targetCityId, targetClubId);
            }}
            className="px-3 py-1.5 rounded-xl bg-red-800 hover:bg-red-700 text-white font-bold text-xs shrink-0 transition-all active:scale-95"
          >
            Повторить
          </button>
        </div>
      )}

      {isLoading ? (
        <div className="py-16 text-center text-xs text-[#8fa89b] animate-pulse">
          Загрузка игр для управления...
        </div>
      ) : !scheduleError && tournaments.length === 0 ? (
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
            const targetCityId = activeCityId === null ? null : activeCityId;
            const targetClubId = targetCityId === null ? null : selectedClubId;
            fetchAdminSchedule(targetCityId, targetClubId);
          }}
        />
      )}
    </div>
  );
};
