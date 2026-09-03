import React, { useState } from 'react';
import { AlertCircle, CheckCircle2, Calendar, Clock, MapPin, Trophy, Users } from 'lucide-react';
import axios from 'axios';
import { tournamentsApi } from '../../api/tournamentsApi';
import { useTournamentsStore } from '../../store/useTournamentsStore';
import { useUserStore } from '../../store/useUserStore';
import { triggerHaptic } from '../../utils/vkBridge';
import { CURRENT_BRANDING } from '../../config/branding';

export const AdminCreateTournamentPanel: React.FC = () => {
  const { fetchAdminSchedule } = useTournamentsStore();
  const { selectedCityId, selectedClubId, setActiveTab } = useUserStore();

  // Инициализируем сегодняшнюю дату в формате DD.MM.YYYY
  const getInitialDate = () => {
    const d = new Date();
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}.${month}.${year}`;
  };

  const [title, setTitle] = useState('Deepstack Tournament');
  const [address, setAddress] = useState(CURRENT_BRANDING.defaultAddress);
  const [date, setDate] = useState(getInitialDate());
  const [time, setTime] = useState('19:00');
  const [format, setFormat] = useState('NL Holdem');
  const [chips, setChips] = useState('15000');
  const [buyIn, setBuyIn] = useState('1500');
  const [maxSeats, setMaxSeats] = useState('30');
  const [regEnd, setRegEnd] = useState('18:45');
  const [description, setDescription] = useState(
    'Регулярный клубный турнир для участников и гостей клуба.\n\n• Стартовый стек — 15 000 фишек\n• Блайнд-апы — 15 минут\n• Поздняя регистрация — 3 часа\n\nRe-Buy\n• 15 000 — 1 500 ₽\n• Premium 30 000 — 2 500 ₽\n\nAdd-on\n• 30 000 — 1 500 ₽'
  );
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [isSuccess, setIsSuccess] = useState(false);

  // Надежный парсер даты и времени
  const parseTournamentDateTime = (dateStr: string, timeStr: string): Date | null => {
    const trimmedDate = dateStr.trim();
    const trimmedTime = timeStr.trim();

    let day: number;
    let month: number;
    let year = new Date().getFullYear();

    // Формат YYYY-MM-DD
    if (/^\d{4}-\d{1,2}-\d{1,2}$/.test(trimmedDate)) {
      const parts = trimmedDate.split('-').map(Number);
      year = parts[0];
      month = parts[1];
      day = parts[2];
    }
    // Формат DD.MM.YYYY
    else if (/^\d{1,2}\.\d{1,2}\.\d{4}$/.test(trimmedDate)) {
      const parts = trimmedDate.split('.').map(Number);
      day = parts[0];
      month = parts[1];
      year = parts[2];
    }
    // Формат DD.MM
    else if (/^\d{1,2}\.\d{1,2}$/.test(trimmedDate)) {
      const parts = trimmedDate.split('.').map(Number);
      day = parts[0];
      month = parts[1];
    } else {
      return null;
    }

    if (month < 1 || month > 12 || day < 1 || day > 31) {
      return null;
    }

    let hour = 19;
    let minute = 0;
    if (trimmedTime.includes(':')) {
      const timeParts = trimmedTime.split(':').map(Number);
      hour = timeParts[0];
      minute = timeParts[1];
      if (isNaN(hour) || isNaN(minute) || hour < 0 || hour > 23 || minute < 0 || minute > 59) {
        return null;
      }
    }

    const parsed = new Date(year, month - 1, day, hour, minute);
    if (isNaN(parsed.getTime())) return null;

    return parsed;
  };

  const validateForm = (): { isValid: boolean; parsedDate: Date | null } => {
    const errors: Record<string, string> = {};

    if (!title.trim() || title.trim().length < 3) {
      errors.title = 'Введите название турнира (не менее 3 символов)';
    }

    const parsedDate = parseTournamentDateTime(date, time);
    if (!parsedDate) {
      errors.date = 'Укажите дату в формате ДД.ММ.ГГГГ или ДД.ММ (например, 15.09)';
      errors.time = 'Укажите время в формате ЧЧ:ММ (например, 19:00)';
    }

    const parsedSeats = parseInt(maxSeats, 10);
    if (isNaN(parsedSeats) || parsedSeats < 2 || parsedSeats > 500) {
      errors.maxSeats = 'Количество мест должно быть от 2 до 500';
    }

    const parsedBuyIn = parseFloat(buyIn);
    if (isNaN(parsedBuyIn) || parsedBuyIn < 0) {
      errors.buyIn = 'Бай-ин не может быть отрицательным';
    }

    const parsedChips = parseInt(chips, 10);
    if (isNaN(parsedChips) || parsedChips <= 0) {
      errors.chips = 'Стартовый стек должен быть больше 0';
    }

    setFieldErrors(errors);

    if (Object.keys(errors).length > 0) {
      setErrorMsg('Пожалуйста, исправьте ошибки в форме перед созданием.');
      return { isValid: false, parsedDate: null };
    }

    return { isValid: true, parsedDate };
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    setFieldErrors({});

    const { isValid, parsedDate } = validateForm();
    if (!isValid || !parsedDate) {
      triggerHaptic('medium');
      return;
    }

    triggerHaptic('heavy');
    setLoading(true);

    try {
      await tournamentsApi.createTournament({
        title: title.trim(),
        clubId: selectedClubId || 1,
        startTime: parsedDate.toISOString(),
        buyIn: parseFloat(buyIn) || 0,
        maxSeats: parseInt(maxSeats, 10) || 30,
        description: description.trim(),
        format: format.trim() || 'NL Holdem',
        startingChips: parseInt(chips, 10) || 15000,
        blindLevelMinutes: 15,
      });

      triggerHaptic('medium');
      setIsSuccess(true);

      // Обновляем список турниров в админке
      await fetchAdminSchedule(selectedCityId, selectedClubId);

      setTimeout(() => {
        setActiveTab('admin-tournaments');
      }, 700);
    } catch (err: unknown) {
      console.error('Ошибка создания турнира:', err);
      triggerHaptic('heavy');
      if (axios.isAxiosError(err)) {
        setErrorMsg(err.response?.data?.message || 'Ошибка сервера при создании турнира.');
      } else if (err instanceof Error) {
        setErrorMsg(err.message);
      } else {
        setErrorMsg('Не удалось создать турнир. Проверьте соединение с сервером.');
      }
      setLoading(false);
    }
  };

  return (
    <div className="px-5 pb-28 animate-fade-in space-y-4">
      {/* Баннер успешного создания */}
      {isSuccess && (
        <div className="p-4 rounded-2xl bg-[#0d2a1f] border border-[#34d399] text-[#a4c9b7] text-xs flex items-center gap-3 animate-fade-in">
          <CheckCircle2 className="w-5 h-5 text-[#34d399] shrink-0" />
          <div>
            <div className="font-bold text-white">Турнир успешно создан!</div>
            <div className="text-[11px] text-[#8fa89b]">Перенаправление в панель управления...</div>
          </div>
        </div>
      )}

      {/* Баннер ошибки */}
      {errorMsg && (
        <div className="p-3.5 rounded-2xl bg-red-950/80 border border-red-500/50 text-red-200 text-xs flex items-start gap-2.5 animate-fade-in shadow-lg">
          <AlertCircle className="w-4 h-4 text-red-400 shrink-0 mt-0.5" />
          <div className="flex-1 font-medium">{errorMsg}</div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Название турнира */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5 flex items-center gap-1">
            <Trophy className="w-3.5 h-3.5 text-[#c39a44]" />
            <span>Название турнира *</span>
          </label>
          <input
            type="text"
            value={title}
            onChange={(e) => {
              setTitle(e.target.value);
              if (fieldErrors.title) setFieldErrors(prev => ({ ...prev, title: '' }));
            }}
            placeholder="Например: Friday Deepstack Bounty"
            className={`w-full py-3 px-4 rounded-2xl bg-black/60 border text-sm font-semibold text-white focus:outline-none transition-all ${
              fieldErrors.title ? 'border-red-500 bg-red-950/20' : 'border-white/10 focus:border-[#c39a44]'
            }`}
          />
          {fieldErrors.title && <p className="text-[11px] text-red-400 mt-1 pl-2">{fieldErrors.title}</p>}
        </div>

        {/* Адрес клуба */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5 flex items-center gap-1">
            <MapPin className="w-3.5 h-3.5 text-[#c39a44]" />
            <span>Адрес проведения</span>
          </label>
          <input
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            className="w-full py-3 px-4 rounded-2xl bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Дата и Время (в одну строку) */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5 flex items-center gap-1">
              <Calendar className="w-3.5 h-3.5 text-[#c39a44]" />
              <span>Дата *</span>
            </label>
            <input
              type="text"
              value={date}
              onChange={(e) => {
                setDate(e.target.value);
                if (fieldErrors.date) setFieldErrors(prev => ({ ...prev, date: '' }));
              }}
              placeholder="ДД.ММ.ГГГГ"
              className={`w-full py-3 px-4 rounded-2xl bg-black/60 border text-sm font-semibold text-white focus:outline-none transition-all ${
                fieldErrors.date ? 'border-red-500 bg-red-950/20' : 'border-white/10 focus:border-[#c39a44]'
              }`}
            />
            {fieldErrors.date && <p className="text-[10px] text-red-400 mt-1">{fieldErrors.date}</p>}
          </div>

          <div>
            <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5 flex items-center gap-1">
              <Clock className="w-3.5 h-3.5 text-[#c39a44]" />
              <span>Время старта *</span>
            </label>
            <input
              type="text"
              value={time}
              onChange={(e) => {
                setTime(e.target.value);
                if (fieldErrors.time) setFieldErrors(prev => ({ ...prev, time: '' }));
              }}
              placeholder="19:00"
              className={`w-full py-3 px-4 rounded-2xl bg-black/60 border text-sm font-semibold text-white focus:outline-none transition-all ${
                fieldErrors.time ? 'border-red-500 bg-red-950/20' : 'border-white/10 focus:border-[#c39a44]'
              }`}
            />
            {fieldErrors.time && <p className="text-[10px] text-red-400 mt-1">{fieldErrors.time}</p>}
          </div>
        </div>

        {/* Параметры игры: Формат, Стек, Бай-ин */}
        <div className="p-4 rounded-3xl bg-black/40 border border-white/10 space-y-3">
          <div className="text-[11px] font-bold text-[#8fa89b] uppercase tracking-wider">
            Параметры игры
          </div>

          <div className="grid grid-cols-3 gap-2">
            <div>
              <label className="block text-[10px] text-[#7d9b8c] mb-1">Формат</label>
              <input
                type="text"
                value={format}
                onChange={(e) => setFormat(e.target.value)}
                placeholder="NL Holdem"
                className="w-full py-2.5 px-3 rounded-xl bg-black/70 border border-white/10 text-xs font-semibold text-white focus:outline-none focus:border-[#c39a44]"
              />
            </div>

            <div>
              <label className="block text-[10px] text-[#7d9b8c] mb-1">Стек (фишек)</label>
              <input
                type="text"
                value={chips}
                onChange={(e) => {
                  setChips(e.target.value.replace(/\D/g, ''));
                  if (fieldErrors.chips) setFieldErrors(prev => ({ ...prev, chips: '' }));
                }}
                placeholder="15000"
                className={`w-full py-2.5 px-3 rounded-xl bg-black/70 border text-xs font-semibold text-white focus:outline-none ${
                  fieldErrors.chips ? 'border-red-500' : 'border-white/10 focus:border-[#c39a44]'
                }`}
              />
            </div>

            <div>
              <label className="block text-[10px] text-[#7d9b8c] mb-1">Бай-ин (₽)</label>
              <input
                type="text"
                value={buyIn}
                onChange={(e) => {
                  setBuyIn(e.target.value.replace(/\D/g, ''));
                  if (fieldErrors.buyIn) setFieldErrors(prev => ({ ...prev, buyIn: '' }));
                }}
                placeholder="1500"
                className={`w-full py-2.5 px-3 rounded-xl bg-black/70 border text-xs font-semibold text-white focus:outline-none ${
                  fieldErrors.buyIn ? 'border-red-500' : 'border-white/10 focus:border-[#c39a44]'
                }`}
              />
            </div>
          </div>
        </div>

        {/* Количество мест и конец регистрации */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5 flex items-center gap-1">
              <Users className="w-3.5 h-3.5 text-[#c39a44]" />
              <span>Лимит мест *</span>
            </label>
            <input
              type="number"
              min="2"
              max="500"
              value={maxSeats}
              onChange={(e) => {
                setMaxSeats(e.target.value);
                if (fieldErrors.maxSeats) setFieldErrors(prev => ({ ...prev, maxSeats: '' }));
              }}
              className={`w-full py-3 px-4 rounded-2xl bg-black/60 border text-sm font-semibold text-white focus:outline-none transition-all ${
                fieldErrors.maxSeats ? 'border-red-500 bg-red-950/20' : 'border-white/10 focus:border-[#c39a44]'
              }`}
            />
            {fieldErrors.maxSeats && <p className="text-[10px] text-red-400 mt-1">{fieldErrors.maxSeats}</p>}
          </div>

          <div>
            <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5 flex items-center gap-1">
              <Clock className="w-3.5 h-3.5 text-[#c39a44]" />
              <span>Конец регистрации</span>
            </label>
            <input
              type="text"
              value={regEnd}
              onChange={(e) => setRegEnd(e.target.value)}
              placeholder="18:45"
              className="w-full py-3 px-4 rounded-2xl bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
            />
          </div>
        </div>

        {/* Описание турнира */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Описание, структура блайндов и Re-Buy
          </label>
          <textarea
            rows={7}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="w-full p-4 rounded-2xl bg-black/60 border border-white/10 text-xs leading-relaxed text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Кнопка сохранить */}
        <div className="pt-2">
          <button
            type="submit"
            disabled={loading || isSuccess}
            className="w-full py-4 px-6 rounded-full bg-[#c39a44] text-white font-bold text-sm shadow-xl shadow-black/60 hover:brightness-105 active:scale-[0.98] transition-all disabled:opacity-50 flex items-center justify-center gap-2"
          >
            {loading ? 'Создание турнира...' : isSuccess ? 'Успешно создано!' : 'Опубликовать турнир'}
          </button>
        </div>
      </form>
    </div>
  );
};
