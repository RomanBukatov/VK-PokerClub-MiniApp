import React, { useEffect, useState } from 'react';
import { ChevronLeft } from 'lucide-react';
import axios from 'axios';
import { tournamentsApi } from '../../api/tournamentsApi';
import { ratingsApi } from '../../api/ratingsApi';
import type { Tournament, TournamentDetail } from '../../types';
import { triggerHaptic } from '../../utils/vkBridge';

interface AdminAssignPointsModalProps {
  tournament: Tournament;
  onClose: () => void;
  onSuccess: () => void;
}

export const AdminAssignPointsModal: React.FC<AdminAssignPointsModalProps> = ({
  tournament,
  onClose,
  onSuccess,
}) => {
  const [detail, setDetail] = useState<TournamentDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [pointsMap, setPointsMap] = useState<Record<number, number>>({});
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  useEffect(() => {
    tournamentsApi.getTournament(tournament.id)
      .then((data) => {
        setDetail(data);
        const initial: Record<number, number> = {};
        data.participants.forEach((p) => {
          // Предзаполняем ранее начисленными очками или 0
          initial[p.userId] = p.pointsEarned ?? 0;
        });
        setPointsMap(initial);
      })
      .catch((err) => {
        console.error('Ошибка загрузки участников:', err);
        setErrorMsg('Не удалось загрузить список участников.');
      })
      .finally(() => setLoading(false));
  }, [tournament.id]);

  const handlePointChange = (userId: number, value: string) => {
    const val = parseInt(value, 10);
    setPointsMap((prev) => ({
      ...prev,
      [userId]: isNaN(val) ? 0 : Math.max(0, val),
    }));
  };

  const applyPresetTop3 = () => {
    triggerHaptic('light');
    if (!detail) return;
    const preset = [100, 60, 30];
    const next: Record<number, number> = {};
    detail.participants.forEach((p, idx) => {
      next[p.userId] = idx < preset.length ? preset[idx] : 0;
    });
    setPointsMap(next);
  };

  const applyPreset6Max = () => {
    triggerHaptic('light');
    if (!detail) return;
    const preset = [100, 70, 50, 35, 20, 10];
    const next: Record<number, number> = {};
    detail.participants.forEach((p, idx) => {
      next[p.userId] = idx < preset.length ? preset[idx] : 0;
    });
    setPointsMap(next);
  };

  const applyPresetEven = () => {
    triggerHaptic('light');
    if (!detail) return;
    const next: Record<number, number> = {};
    detail.participants.forEach((p) => {
      next[p.userId] = 10;
    });
    setPointsMap(next);
  };

  // Проверка на потенциальную опечатку (человеческий фактор: > 5000 очков)
  const hasSuspiciouslyHighPoints = Object.values(pointsMap).some((pts) => pts > 5000);

  const handleSubmit = async () => {
    triggerHaptic('heavy');
    setSubmitting(true);
    setErrorMsg(null);

    try {
      await ratingsApi.assignPoints(tournament.id, pointsMap);
      triggerHaptic('medium');
      onSuccess();
    } catch (err: unknown) {
      console.error('Ошибка начисления очков:', err);
      if (axios.isAxiosError(err)) {
        setErrorMsg(err.response?.data?.message || 'Ошибка сервера при начислении очков.');
      } else {
        setErrorMsg('Непредвиденная ошибка при начислении очков.');
      }
      setSubmitting(false);
    }
  };

  const getInitials = (first?: string, last?: string) => {
    const f = first?.[0] || 'А';
    const l = last?.[0] || 'К';
    return `${f}${l}`.toUpperCase();
  };

  const formatSubtitleDate = (dateStr: string) => {
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });
    } catch {
      return 'Недавно';
    }
  };

  const openVkProfile = (vkId: string) => {
    triggerHaptic('light');
    if (vkId && !vkId.startsWith('100')) {
      window.open(`https://vk.com/id${vkId}`, '_blank');
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-[#01201a] overflow-y-auto animate-fade-in flex flex-col justify-between p-5 pb-8 safe-bottom">
      <div>
        {/* Верхняя шапка */}
        <div className="flex items-center gap-2 mb-4 safe-top">
          <button
            onClick={() => { triggerHaptic('light'); onClose(); }}
            className="p-2 -ml-2 text-white hover:opacity-80 active:scale-95"
          >
            <ChevronLeft className="w-7 h-7" />
          </button>
          <div>
            <h1 className="text-2xl font-extrabold text-white">
              {tournament.status === 3 ? 'Корректировка очков' : 'Начисление очков'}
            </h1>
            <p className="text-xs text-[#8fa89b] mt-0.5">
              {tournament.title} · {formatSubtitleDate(tournament.startTime)}
            </p>
          </div>
        </div>

        {errorMsg && (
          <div className="mb-4 p-3 rounded-2xl bg-red-950/80 border border-red-500/40 text-red-400 text-xs">
            {errorMsg}
          </div>
        )}

        {/* Защита от человеческого фактора: предупреждение при высоком числе очков */}
        {hasSuspiciouslyHighPoints && (
          <div className="mb-4 p-3 rounded-2xl bg-amber-950/80 border border-amber-500/40 text-amber-300 text-xs flex items-center gap-2">
            <span>⚠️</span>
            <span>Внимание: введено более 5 000 очков одному из игроков. Убедитесь, что нет опечатки в количестве нулей.</span>
          </div>
        )}

        {/* Быстрые шаблоны начисления */}
        <div className="mb-4 p-3 rounded-2xl bg-black/40 border border-white/10 space-y-2">
          <div className="text-[11px] font-bold text-[#8fa89b] uppercase tracking-wider">
            Быстрые шаблоны:
          </div>
          <div className="flex gap-2">
            <button
              onClick={applyPresetTop3}
              type="button"
              className="flex-1 py-1.5 px-2 rounded-full bg-black/60 border border-[#1a3b2b] text-[11px] font-bold text-white hover:border-[#c39a44] active:scale-95 transition-all"
            >
              🏆 Топ-3
            </button>
            <button
              onClick={applyPreset6Max}
              type="button"
              className="flex-1 py-1.5 px-2 rounded-full bg-black/60 border border-[#1a3b2b] text-[11px] font-bold text-white hover:border-[#c39a44] active:scale-95 transition-all"
            >
              ⚡ 6-Max
            </button>
            <button
              onClick={applyPresetEven}
              type="button"
              className="flex-1 py-1.5 px-2 rounded-full bg-black/60 border border-[#1a3b2b] text-[11px] font-bold text-white hover:border-[#c39a44] active:scale-95 transition-all"
            >
              🎯 По 10 всем
            </button>
          </div>
        </div>

        {/* Заголовки таблицы */}
        <div className="flex items-center justify-between px-3 text-[10px] font-bold text-[#7d9b8c] uppercase tracking-wider mb-2">
          <span>УЧАСТНИК (РЕЦЕПШЕН)</span>
          <span>ОЧКИ</span>
        </div>

        {/* Список игроков с полями ввода и контактами */}
        {loading ? (
          <div className="py-12 text-center text-xs text-[#8fa89b] animate-pulse">
            Загрузка списка игроков...
          </div>
        ) : !detail || detail.participants.length === 0 ? (
          <div className="py-8 text-center text-xs text-[#8fa89b] bg-black/40 rounded-3xl p-6 border border-white/5">
            На этот турнир не было зарегистрировано ни одного игрока.
          </div>
        ) : (
          <div className="space-y-2.5">
            {detail.participants.map((p, idx) => (
              <div
                key={p.userId}
                className="p-3 px-4 rounded-2xl bg-black/40 border border-white/10 flex items-center justify-between shadow-md gap-2"
              >
                <div className="flex items-center gap-3 min-w-0 flex-1">
                  <span className="w-4 text-xs font-bold text-gray-400 text-center shrink-0">
                    #{idx + 1}
                  </span>
                  <div className="w-8 h-8 rounded-full bg-[#606a66] flex items-center justify-center font-bold text-xs text-white overflow-hidden shrink-0">
                    {p.avatarUrl ? (
                      <img src={p.avatarUrl} alt="" className="w-full h-full object-cover" />
                    ) : (
                      getInitials(p.firstName, p.lastName)
                    )}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="text-sm font-semibold text-white truncate">
                      {p.firstName} {p.lastName}
                    </div>
                    <div className="flex items-center gap-2 mt-0.5">
                      <button
                        type="button"
                        onClick={() => openVkProfile(p.vkId)}
                        className="text-[10px] text-[#7d9b8c] hover:text-[#c39a44] transition-colors truncate flex items-center gap-1"
                        title="Открыть VK профиль"
                      >
                        <span>VK ID: {p.vkId}</span>
                        <span className="text-[#c39a44] text-[9px]">↗</span>
                      </button>
                      <span className="text-[10px] text-[#46625b]">· Рейтинг: {p.totalRating}</span>
                    </div>
                  </div>
                </div>

                <div className="relative shrink-0">
                  <input
                    type="number"
                    min="0"
                    max="100000"
                    value={pointsMap[p.userId] ?? 0}
                    onChange={(e) => handlePointChange(p.userId, e.target.value)}
                    className={`w-24 py-1.5 px-3 rounded-full bg-black/80 border text-center font-bold text-sm text-white focus:outline-none ${
                      (pointsMap[p.userId] ?? 0) > 5000
                        ? 'border-amber-500 text-amber-300'
                        : 'border-[#46625b] focus:border-[#c39a44]'
                    }`}
                  />
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Нижняя кнопка */}
      <div className="pt-6">
        <button
          onClick={handleSubmit}
          disabled={submitting || !detail || detail.participants.length === 0}
          className="w-full py-4 px-6 rounded-full bg-[#c39a44] text-white font-bold text-sm shadow-xl hover:brightness-105 active:scale-[0.98] transition-all disabled:opacity-50"
        >
          {submitting ? 'Пересчет рейтинга...' : tournament.status === 3 ? 'Скорректировать и пересчитать рейтинг' : 'Сохранить и начислить рейтинг'}
        </button>
      </div>
    </div>
  );
};
