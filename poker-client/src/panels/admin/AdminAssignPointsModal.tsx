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
        data.participants.forEach((p, idx) => {
          // Инициализируем стандартными очками
          initial[p.userId] = idx === 0 ? 2840 : idx === 1 ? 1800 : idx === 2 ? 1000 : 200;
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
              Начисление очков
            </h1>
            <p className="text-xs text-[#8fa89b] mt-0.5">
              {tournament.title} · 18 июля
            </p>
          </div>
        </div>

        {errorMsg && (
          <div className="mb-4 p-3 rounded-2xl bg-red-950/80 border border-red-500/40 text-red-400 text-xs">
            {errorMsg}
          </div>
        )}

        {/* Заголовки таблицы */}
        <div className="flex items-center justify-between px-3 text-[10px] font-bold text-[#7d9b8c] uppercase tracking-wider mb-2">
          <span>ИГРОК</span>
          <span>ОЧКИ</span>
        </div>

        {/* Список игроков с полями ввода */}
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
            {detail.participants.map((p) => (
              <div
                key={p.userId}
                className="p-3 px-4 rounded-2xl bg-black/40 border border-white/10 flex items-center justify-between shadow-md"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full bg-[#606a66] flex items-center justify-center font-bold text-xs text-white overflow-hidden shrink-0">
                    {p.avatarUrl ? (
                      <img src={p.avatarUrl} alt="" className="w-full h-full object-cover" />
                    ) : (
                      getInitials(p.firstName, p.lastName)
                    )}
                  </div>
                  <span className="text-sm font-semibold text-white">
                    {p.firstName} {p.lastName}
                  </span>
                </div>

                <div className="relative">
                  <input
                    type="number"
                    min="0"
                    value={pointsMap[p.userId] ?? 0}
                    onChange={(e) => handlePointChange(p.userId, e.target.value)}
                    className="w-24 py-1.5 px-3 rounded-full bg-black/80 border border-[#46625b] text-center font-bold text-sm text-white focus:outline-none focus:border-[#c39a44]"
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
          {submitting ? 'Сохранение...' : 'Сохранить и начислить рейтинг'}
        </button>
      </div>
    </div>
  );
};
