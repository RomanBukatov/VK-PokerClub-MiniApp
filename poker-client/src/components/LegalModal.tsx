import React, { useState } from 'react';
import { ShieldCheck, FileText, ChevronDown, ChevronUp, Check, AlertCircle } from 'lucide-react';
import { useUserStore } from '../store/useUserStore';
import { triggerHaptic } from '../utils/vkBridge';

export const LegalModal: React.FC = () => {
  const { isLegalModalOpen, acceptTerms } = useUserStore();

  const [agree152, setAgree152] = useState(false);
  const [agreeOffer, setAgreeOffer] = useState(false);
  const [showFullTerms, setShowFullTerms] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!isLegalModalOpen) return null;

  const canAccept = agree152 && agreeOffer;

  const handleAccept = async () => {
    if (!canAccept || isSubmitting) return;
    triggerHaptic('medium');
    setIsSubmitting(true);
    try {
      await acceptTerms();
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-md animate-fade-in select-none">
      <div className="w-full max-w-md bg-[#0d281e] border border-[#c39a44]/30 rounded-3xl p-6 shadow-2xl shadow-black/80 flex flex-col max-h-[90vh] overflow-hidden text-white">
        {/* Иконка и заголовок */}
        <div className="flex items-center gap-3.5 mb-4 shrink-0">
          <div className="w-12 h-12 rounded-2xl bg-[#c39a44]/20 border border-[#c39a44]/40 flex items-center justify-center text-[#c39a44]">
            <ShieldCheck className="w-6 h-6" />
          </div>
          <div>
            <h2 className="text-lg font-black tracking-tight text-white leading-tight">
              Правила клуба и Оферта
            </h2>
            <p className="text-xs text-[#8fa89b]">
              Monte Carlo Poker Club · Правовой регламент
            </p>
          </div>
        </div>

        {/* Информационный баннер о 244-ФЗ */}
        <div className="p-3.5 rounded-2xl bg-black/40 border border-white/5 mb-4 shrink-0 text-xs text-[#d1e0d7] space-y-1.5">
          <div className="flex items-center gap-2 text-[#c39a44] font-bold">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>Любительский карточный досуг</span>
          </div>
          <p className="text-[11px] leading-relaxed text-[#8fa89b]">
            Деятельность клуба носит исключительно спортивно-досуговый характер согласно <strong className="text-white">244-ФЗ</strong>. В клубе не проводятся азартные игры на деньги и пари. Рейтинг (RPS) определяет спортивные достижения.
          </p>
        </div>

        {/* Раскрывающийся блок с полным текстом оферты */}
        <div className="flex-1 overflow-y-auto pr-1 space-y-3 mb-4 custom-scrollbar">
          <button
            type="button"
            onClick={() => {
              triggerHaptic('light');
              setShowFullTerms(!showFullTerms);
            }}
            className="w-full py-2 px-3 rounded-xl bg-black/30 hover:bg-black/50 border border-white/10 flex items-center justify-between text-xs font-semibold text-[#c39a44] transition-all"
          >
            <span className="flex items-center gap-2">
              <FileText className="w-4 h-4" />
              {showFullTerms ? 'Скрыть текст Договора оферты' : 'Читать полный текст Договора оферты'}
            </span>
            {showFullTerms ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>

          {showFullTerms && (
            <div className="p-3.5 rounded-2xl bg-black/50 border border-white/5 text-[11px] text-[#a1b8ac] space-y-2.5 max-h-56 overflow-y-auto leading-relaxed">
              <p className="font-bold text-white text-xs">
                ПОЛОЖЕНИЕ О ПРОВЕДЕНИИ КЛУБНЫХ ТУРНИРОВ ПО СПОРТИВНОМУ ПОКЕРУ (ПУБЛИЧНАЯ ОФЕРТА)
              </p>
              <p>
                1. <strong>Общие положения:</strong> Настоящий Договор является публичной офертой любительского карточного клуба «Monte Carlo». Принятие условий означает полное согласие участника с правилами клуба.
              </p>
              <p>
                2. <strong>Правовой статус (244-ФЗ):</strong> Клубные турниры не являются азартными играми. Турниры проводятся исключительно на игровых фишках без денежного эквивалента в рамках спортивного и культурно-досугового досуга. Любые денежные ставки, пари и расчеты между участниками категорически запрещены.
              </p>
              <p>
                3. <strong>Рейтинг и Очки:</strong> Участники турниров получают рейтинговые очки клуба (RPS), которые служат для присвоения клубного статуса (Newbie, Fish, Reg, Pro) и формирования турнирных сеток.
              </p>
              <p>
                4. <strong>152-ФЗ (Персональные данные):</strong> Настоящим участник дает согласие на обработку персональных данных (ФИО, телефон, игровой никнейм, ID в соцсетях) исключительно в целях ведения турнирных таблиц, связи и клубного учета.
              </p>
              <p>
                5. <strong>Правила этикета:</strong> Участники обязуются соблюдать спортивную этику, уважение к дилерам и соперникам. Нарушение правил влечет дисквалификацию без сохранения очков.
              </p>
            </div>
          )}

          {/* Чекбоксы согласия */}
          <div className="space-y-2.5 pt-1">
            {/* Чекбокс 1: 152-ФЗ */}
            <label className="flex items-start gap-3 p-3 rounded-2xl bg-black/30 border border-white/5 hover:border-white/15 cursor-pointer transition-all">
              <input
                type="checkbox"
                className="sr-only"
                checked={agree152}
                onChange={(e) => {
                  triggerHaptic('light');
                  setAgree152(e.target.checked);
                }}
              />
              <div
                className={`w-5 h-5 rounded-lg border flex items-center justify-center mt-0.5 shrink-0 transition-all ${
                  agree152
                    ? 'bg-[#c39a44] border-[#c39a44] text-black shadow-md shadow-[#c39a44]/30'
                    : 'border-white/30 bg-black/40'
                }`}
              >
                {agree152 && <Check className="w-3.5 h-3.5 stroke-[3]" />}
              </div>
              <span className="text-xs text-[#d1e0d7] select-none leading-snug">
                Согласен на обработку персональных данных (<strong className="text-white">152-ФЗ</strong>)
              </span>
            </label>

            {/* Чекбокс 2: Договор оферты */}
            <label className="flex items-start gap-3 p-3 rounded-2xl bg-black/30 border border-white/5 hover:border-white/15 cursor-pointer transition-all">
              <input
                type="checkbox"
                className="sr-only"
                checked={agreeOffer}
                onChange={(e) => {
                  triggerHaptic('light');
                  setAgreeOffer(e.target.checked);
                }}
              />
              <div
                className={`w-5 h-5 rounded-lg border flex items-center justify-center mt-0.5 shrink-0 transition-all ${
                  agreeOffer
                    ? 'bg-[#c39a44] border-[#c39a44] text-black shadow-md shadow-[#c39a44]/30'
                    : 'border-white/30 bg-black/40'
                }`}
              >
                {agreeOffer && <Check className="w-3.5 h-3.5 stroke-[3]" />}
              </div>
              <span className="text-xs text-[#d1e0d7] select-none leading-snug">
                Принимаю <strong className="text-white">Договор оферты</strong> (положение о любительском покере)
              </span>
            </label>
          </div>
        </div>

        {/* Кнопка подтверждения */}
        <div className="shrink-0 pt-2 border-t border-white/10">
          <button
            type="button"
            disabled={!canAccept || isSubmitting}
            onClick={handleAccept}
            className={`w-full py-3.5 px-5 rounded-2xl font-extrabold text-sm flex items-center justify-center gap-2 transition-all ${
              canAccept && !isSubmitting
                ? 'bg-gradient-to-r from-[#d8af56] to-[#b38833] text-black shadow-lg shadow-[#c39a44]/30 hover:brightness-105 active:scale-[0.98]'
                : 'bg-white/10 text-[#606a66] cursor-not-allowed border border-white/5'
            }`}
          >
            {isSubmitting ? (
              <span className="animate-pulse">Вход в клуб...</span>
            ) : (
              <span>Принять условия и войти в клуб</span>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
