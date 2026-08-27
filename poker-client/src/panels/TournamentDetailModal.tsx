import React, { useState } from 'react';
import { ChevronLeft } from 'lucide-react';
import { useTournamentsStore } from '../store/useTournamentsStore';
import { useUserStore } from '../store/useUserStore';
import { formatCurrency, formatChips } from '../utils/formatters';
import { triggerHaptic } from '../utils/vkBridge';
import logoSvg from '../assets/logo.svg';
import chipGoldImg from '../assets/chip_gold.png';
import cardsBgImg from '../assets/cards_bg.png';

export const TournamentDetailModal: React.FC = () => {
  const { 
    selectedTournament, 
    isDetailModalOpen, 
    closeDetail, 
    registerToTournament, 
    unregisterFromTournament,
    isActionLoading 
  } = useTournamentsStore();
  const { vkUser } = useUserStore();

  const [isCancelConfirmOpen, setIsCancelConfirmOpen] = useState(false);

  if (!isDetailModalOpen || !selectedTournament) return null;

  const t = selectedTournament;
  const isRegistered = t.isUserRegistered || 
    (vkUser && t.participants.some(p => p.vkId === vkUser.id.toString()));

  const handleRegister = async () => {
    triggerHaptic('heavy');
    await registerToTournament(t.id);
  };

  const handleConfirmUnregister = async () => {
    setIsCancelConfirmOpen(false);
    triggerHaptic('medium');
    await unregisterFromTournament(t.id);
  };

  return (
    <div className="fixed inset-0 z-50 bg-[#01201a] overflow-y-auto animate-fade-in flex flex-col justify-between">
      {/* 1. Экран «Место подтверждено» */}
      {isRegistered ? (
        <div className="min-h-screen relative flex flex-col justify-between p-5 overflow-hidden">
          {/* Фоновая графика */}
          <img
            src={chipGoldImg}
            alt=""
            className="absolute -top-10 -left-12 w-44 h-44 object-contain pointer-events-none opacity-80 filter drop-shadow-2xl"
          />
          <img
            src={cardsBgImg}
            alt=""
            className="absolute -bottom-16 -right-10 w-72 h-96 object-contain pointer-events-none opacity-70 filter drop-shadow-2xl"
          />

          {/* Верхняя панель */}
          <div className="flex items-center justify-between z-10 safe-top">
            <button
              onClick={() => { triggerHaptic('light'); closeDetail(); }}
              className="p-2 -ml-2 text-white hover:opacity-80 active:scale-95"
            >
              <ChevronLeft className="w-7 h-7" />
            </button>
            <img src={logoSvg} alt="Monte Carlo" className="h-8 object-contain" />
          </div>

          {/* Центральный заголовок */}
          <div className="my-auto z-10 text-center py-4">
            <h1 className="text-3xl font-extrabold text-white mb-6">
              Место<br />подтверждено
            </h1>

            {/* Карточка турнира */}
            <div className="max-w-xs mx-auto p-5 rounded-3xl bg-black/60 border border-white/10 text-left shadow-2xl space-y-3">
              <div>
                <div className="text-[10px] uppercase font-bold text-[#8fa89b]">ТУРНИР</div>
                <div className="text-base font-bold text-white mt-0.5">{t.title}</div>
              </div>

              <div className="flex flex-wrap gap-1.5">
                <span className="px-2.5 py-0.5 rounded-full text-[11px] font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                  {t.format || 'no limit'}
                </span>
                <span className="px-2.5 py-0.5 rounded-full text-[11px] font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                  стартовый стек {formatChips(t.startingChips || 10000)}
                </span>
                <span className="px-2.5 py-0.5 rounded-full text-[11px] font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                  {formatCurrency(t.buyIn)}
                </span>
              </div>

              <div>
                <div className="text-[10px] uppercase font-bold text-[#8fa89b]">АДРЕС</div>
                <div className="text-xs font-semibold text-white mt-0.5">
                  {t.clubAddress || 'Монастырская улица, 59, Пермь'}
                </div>
              </div>

              <div>
                <div className="text-[10px] uppercase font-bold text-[#8fa89b]">ВРЕМЯ</div>
                <div className="text-sm font-bold text-white mt-0.5">
                  19:00
                </div>
              </div>
            </div>

            {/* Кнопка отмены записи */}
            <div className="mt-8">
              <button
                onClick={() => { triggerHaptic('light'); setIsCancelConfirmOpen(true); }}
                className="py-3 px-8 rounded-full bg-[#363f3a] text-white font-bold text-xs hover:bg-[#46524c] active:scale-95 transition-all shadow-lg"
              >
                Отменить запись
              </button>
            </div>
          </div>

          <div className="pb-6" />
        </div>
      ) : (
        /* 2. Экран детальной карточки турнира */
        <div className="min-h-screen flex flex-col justify-between p-5 pb-8">
          <div>
            {/* Верхняя шапка со стрелкой и логотипом */}
            <div className="flex items-center justify-between mb-4 safe-top">
              <button
                onClick={() => { triggerHaptic('light'); closeDetail(); }}
                className="p-2 -ml-2 text-white hover:opacity-80 active:scale-95"
              >
                <ChevronLeft className="w-7 h-7" />
              </button>
              <img src={logoSvg} alt="Monte Carlo" className="h-8 object-contain" />
            </div>

            {/* Название и дата */}
            <h1 className="text-2xl font-extrabold text-white mb-1">
              {t.title}
            </h1>
            <div className="text-xs font-bold text-[#d1e0d7] uppercase tracking-wider mb-3">
              СЕГОДНЯ · 19:00
            </div>

            {/* Пиллы параметров */}
            <div className="flex flex-wrap gap-2 mb-4">
              <span className="px-3 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                {t.format || 'no limit'}
              </span>
              <span className="px-3 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                стартовый стек {formatChips(t.startingChips || 10000)}
              </span>
              <span className="px-3 py-1 rounded-full text-xs font-semibold text-white bg-black/70 border border-[#1a3b2b]">
                {formatCurrency(t.buyIn)}
              </span>
            </div>

            {/* Основной блок деталей */}
            <div className="p-5 rounded-3xl bg-black/50 border border-white/10 shadow-xl space-y-4">
              {/* Места */}
              <div>
                <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1">
                  МЕСТА
                </div>
                <div className="flex items-center gap-3">
                  <span className="text-sm font-bold text-white">
                    {t.registeredCount >= t.maxSeats ? 'Мест не осталось' : 'Много мест'}
                  </span>
                  <div className="flex-1 h-2 rounded-full bg-[#132c20] overflow-hidden">
                    <div
                      className="h-full rounded-full bg-[#3d6550]"
                      style={{ width: `${Math.min(100, Math.round((t.registeredCount / t.maxSeats) * 100))}%` }}
                    />
                  </div>
                </div>
              </div>

              {/* Адрес */}
              <div>
                <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-0.5">
                  АДРЕС
                </div>
                <div className="text-sm font-bold text-white">
                  {t.clubAddress || 'Монастырская улица, 59, Пермь'}
                </div>
              </div>

              {/* Конец регистрации */}
              <div>
                <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-0.5">
                  КОНЕЦ РЕГИСТРАЦИИ
                </div>
                <div className="text-sm font-bold text-white">
                  в 18:45
                </div>
              </div>

              {/* О турнире */}
              <div>
                <div className="text-[10px] uppercase font-bold text-[#8fa89b] tracking-wider mb-1">
                  О ТУРНИРЕ
                </div>
                <p className="text-xs text-white/90 leading-relaxed">
                  {t.description || (
                    <>
                      Бесплатный турнир для всех желающих.<br /><br />
                      • Стартовый стек — 10 000 фишек<br />
                      • Блайнд-апы — 15 минут<br />
                      • Поздняя регистрация — 3 часа<br /><br />
                      Re-Buy<br />
                      • 20 000 — 1000 ₽<br />
                      • Premium 40 000 — 1500 ₽<br /><br />
                      Add-on<br />
                      • 20 000 — 500 ₽<br />
                      • 40 000 — 1000 ₽<br />
                      • 60 000 — 1500 ₽ + 🍺
                    </>
                  )}
                </p>
              </div>
            </div>
          </div>

          {/* Кнопка регистрации */}
          <div className="pt-4">
            <button
              onClick={handleRegister}
              disabled={isActionLoading || t.registeredCount >= t.maxSeats}
              className="w-full py-4 px-6 rounded-full bg-[#c39a44] text-white font-bold text-base shadow-xl hover:brightness-105 active:scale-[0.98] transition-all disabled:opacity-50"
            >
              {isActionLoading ? 'Запись...' : 'Зарегистрироваться'}
            </button>
          </div>
        </div>
      )}

      {/* 3. Модальное окно «Отменить запись?» */}
      {isCancelConfirmOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-6 bg-black/85 backdrop-blur-md animate-fade-in">
          <div className="w-full max-w-xs p-6 rounded-3xl bg-[#091510] border border-white/10 text-center shadow-2xl space-y-4">
            <h2 className="text-xl font-extrabold text-white">
              Отменить запись?
            </h2>
            <p className="text-xs text-[#8fa89b] leading-relaxed">
              Ваше место станет доступно другим игрокам. Вернуть запись автоматически не получится.
            </p>
            <div className="flex gap-2.5 pt-2">
              <button
                onClick={handleConfirmUnregister}
                disabled={isActionLoading}
                className="flex-1 py-3 px-3 rounded-full bg-[#444444] text-white font-bold text-xs hover:bg-[#555555] active:scale-95 transition-all"
              >
                Отменить
              </button>
              <button
                onClick={() => setIsCancelConfirmOpen(false)}
                disabled={isActionLoading}
                className="flex-1 py-3 px-3 rounded-full bg-[#c39a44] text-white font-bold text-xs hover:brightness-105 active:scale-95 transition-all"
              >
                Не отменять
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
