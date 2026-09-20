import React, { useState } from 'react';
import { AlertCircle, CheckCircle2, Calendar, Clock, MapPin, Trophy, Users, Pencil, ArrowLeft } from 'lucide-react';
import axios from 'axios';
import { tournamentsApi } from '../../api/tournamentsApi';
import { useTournamentsStore } from '../../store/useTournamentsStore';
import { useUserStore } from '../../store/useUserStore';
import { triggerHaptic } from '../../utils/vkBridge';
import { CURRENT_BRANDING } from '../../config/branding';

export const AdminCreateTournamentPanel: React.FC = () => {
  const { editingTournament } = useTournamentsStore();
  return <AdminCreateTournamentForm key={editingTournament ? `edit-${editingTournament.id}` : 'create'} />;
};

const AdminCreateTournamentForm: React.FC = () => {
  const { fetchAdminSchedule, editingTournament, setEditingTournament, updateTournament } = useTournamentsStore();
  const { selectedCityId, selectedCity, selectedClubId, setActiveTab } = useUserStore();

  // Инициализируем сегодняшнюю дату в формате DD.MM.YYYY
  const getInitialDate = () => {
    const d = new Date();
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}.${month}.${year}`;
  };

  const [title, setTitle] = useState(() => editingTournament?.title || 'Deepstack Tournament');
  const [address, setAddress] = useState(() => editingTournament?.clubAddress || CURRENT_BRANDING.defaultAddress);
  const [date, setDate] = useState(() => {
    if (editingTournament?.startTime) {
      const d = new Date(editingTournament.startTime);
      if (!isNaN(d.getTime())) {
        const day = String(d.getDate()).padStart(2, '0');
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const year = d.getFullYear();
        return `${day}.${month}.${year}`;
      }
    }
    return getInitialDate();
  });
  const [time, setTime] = useState(() => {
    if (editingTournament?.startTime) {
      const d = new Date(editingTournament.startTime);
      if (!isNaN(d.getTime())) {
        const hours = String(d.getHours()).padStart(2, '0');
        const minutes = String(d.getMinutes()).padStart(2, '0');
        return `${hours}:${minutes}`;
      }
    }
    return '19:00';
  });
  const [format, setFormat] = useState(() => editingTournament?.format || 'NL Holdem');
  const [chips, setChips] = useState(() => editingTournament?.startingChips ? String(editingTournament.startingChips) : '15000');
  const [buyIn, setBuyIn] = useState(() => editingTournament?.buyIn !== undefined ? String(editingTournament.buyIn) : '1500');
  const [maxSeats, setMaxSeats] = useState(() => editingTournament?.maxSeats ? String(editingTournament.maxSeats) : '30');
  const [regEnd, setRegEnd] = useState(() => {
    if (editingTournament?.registrationEnd) {
      const rd = new Date(editingTournament.registrationEnd);
      if (!isNaN(rd.getTime())) {
        const hours = String(rd.getHours()).padStart(2, '0');
        const minutes = String(rd.getMinutes()).padStart(2, '0');
        return `${hours}:${minutes}`;
      }
    }
    return editingTournament ? '' : '18:45';
  });
  const [description, setDescription] = useState(
    () => editingTournament
      ? (editingTournament.description ?? '')
      : 'Регулярный клубный турнир для участников и гостей клуба.\n\n• Стартовый стек — 15 000 фишек\n• Блайнд-апы — 15 минут\n• Поздняя регистрация — 3 часа\n\nRe-Buy\n• 15 000 — 1 500 ₽\n• Premium 30 000 — 2 500 ₽\n\nAdd-on\n• 30 000 — 1 500 ₽'
  );
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [isSuccess, setIsSuccess] = useState(false);

  // Надежный парсер даты и времени
  const parseTournamentDateTime = (dateStr: string, timeStr: string): Date | null => {
    const cleanDate = dateStr.trim().replace(/\s*([.\-/])\s*/g, '$1');
    const cleanTime = timeStr.trim();

    let day: number;
    let month: number;
    let year = new Date().getFullYear();

    // Формат YYYY-MM-DD или YYYY.MM.DD или YYYY/MM/DD
    if (/^\d{4}[.\-/]\d{1,2}[.\-/]\d{1,2}$/.test(cleanDate)) {
      const parts = cleanDate.split(/[.\-/]/).map(Number);
      year = parts[0];
      month = parts[1];
      day = parts[2];
    }
    // Формат DD.MM.YYYY или DD/MM/YYYY или DD-MM-YYYY (с поддержкой 2-значного года DD.MM.YY)
    else if (/^\d{1,2}[.\-/]\d{1,2}[.\-/]\d{2,4}$/.test(cleanDate)) {
      const parts = cleanDate.split(/[.\-/]/).map(Number);
      day = parts[0];
      month = parts[1];
      year = parts[2];
      if (year < 100) {
        year += 2000;
      }
    }
    // Формат DD.MM или DD/MM или DD-MM (год = текущий)
    else if (/^\d{1,2}[.\-/]\d{1,2}$/.test(cleanDate)) {
      const parts = cleanDate.split(/[.\-/]/).map(Number);
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
    if (cleanTime) {
      const normalizedTime = cleanTime.replace(/\s+/g, '');
      if (/^\d{1,2}[:.-]\d{1,2}$/.test(normalizedTime)) {
        const timeParts = normalizedTime.split(/[:.-]/).map(Number);
        hour = timeParts[0];
        minute = timeParts[1];
      } else if (/^\d{1,2}$/.test(normalizedTime)) {
        hour = Number(normalizedTime);
        minute = 0;
      } else {
        return null;
      }
    }

    if (isNaN(hour) || isNaN(minute) || hour < 0 || hour > 23 || minute < 0 || minute > 59) {
      return null;
    }

    const parsed = new Date(year, month - 1, day, hour, minute);
    if (isNaN(parsed.getTime())) return null;

    // Защита от переполнения даты (например, 31 февраля или 31 апреля)
    if (parsed.getDate() !== day || parsed.getMonth() !== month - 1 || parsed.getFullYear() !== year) {
      return null;
    }

    return parsed;
  };

  const validateForm = (): { isValid: boolean; parsedDate: Date | null; parsedRegEnd: Date | null } => {
    const errors: Record<string, string> = {};

    if (!title.trim() || title.trim().length < 3) {
      errors.title = 'Введите название турнира (не менее 3 символов)';
    }

    const parsedDate = parseTournamentDateTime(date, time);
    if (!parsedDate) {
      errors.date = 'Укажите дату в формате ДД.ММ.ГГГГ или ДД.ММ (например, 15.09)';
      errors.time = 'Укажите время в формате ЧЧ:ММ (например, 19:00)';
    }

    let parsedRegEnd: Date | null = null;
    if (regEnd.trim()) {
      const parts = regEnd.trim().split(/\s+/);
      if (parts.length >= 2) {
        parsedRegEnd = parseTournamentDateTime(parts[0], parts[1]);
      }
      if (!parsedRegEnd) {
        parsedRegEnd = parseTournamentDateTime(date, regEnd.trim());
      }
      if (!parsedRegEnd) {
        errors.regEnd = 'Укажите время окончания регистрации в формате ЧЧ:ММ (например, 18:45)';
      } else if (parsedDate && parsedRegEnd < parsedDate) {
        const diffMs = parsedDate.getTime() - parsedRegEnd.getTime();
        if (diffMs > 6 * 3600 * 1000) {
          parsedRegEnd.setDate(parsedRegEnd.getDate() + 1);
        }
      }
    }

    const parsedSeats = parseInt(maxSeats, 10);
    if (isNaN(parsedSeats) || parsedSeats < 2 || parsedSeats > 500) {
      errors.maxSeats = 'Количество мест должно быть от 2 до 500';
    } else if (editingTournament && (editingTournament.registeredCount || 0) > parsedSeats) {
      errors.maxSeats = `Количество мест (${parsedSeats}) не может быть меньше числа уже записанных игроков (${editingTournament.registeredCount})`;
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
      setErrorMsg(editingTournament ? 'Пожалуйста, исправьте ошибки в форме перед сохранением.' : 'Пожалуйста, исправьте ошибки в форме перед созданием.');
      return { isValid: false, parsedDate: null, parsedRegEnd: null };
    }

    return { isValid: true, parsedDate, parsedRegEnd };
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    setFieldErrors({});

    const { isValid, parsedDate, parsedRegEnd } = validateForm();
    if (!isValid || !parsedDate) {
      triggerHaptic('medium');
      return;
    }

    triggerHaptic('heavy');
    setLoading(true);

    try {
      if (editingTournament) {
        const ok = await updateTournament(editingTournament.id, {
          title: title.trim(),
          clubId: editingTournament.clubId,
          address: address.trim() || undefined,
          startTime: parsedDate.toISOString(),
          registrationEnd: parsedRegEnd ? parsedRegEnd.toISOString() : null,
          clearRegistrationEnd: !parsedRegEnd,
          buyIn: parseFloat(buyIn) || 0,
          maxSeats: parseInt(maxSeats, 10) || 30,
          description: description.trim(),
          format: format.trim() || 'NL Holdem',
          startingChips: parseInt(chips, 10) || 15000,
          blindLevelMinutes: 15,
        });

        if (!ok) {
          const err = useTournamentsStore.getState().actionError;
          setErrorMsg(err || 'Не удалось сохранить изменения турнира.');
          setLoading(false);
          return;
        }
      } else {
        await tournamentsApi.createTournament({
          title: title.trim(),
          clubId: selectedClubId || undefined,
          cityId: selectedCityId || undefined,
          address: address.trim() || undefined,
          startTime: parsedDate.toISOString(),
          registrationEnd: parsedRegEnd ? parsedRegEnd.toISOString() : undefined,
          buyIn: parseFloat(buyIn) || 0,
          maxSeats: parseInt(maxSeats, 10) || 30,
          description: description.trim(),
          format: format.trim() || 'NL Holdem',
          startingChips: parseInt(chips, 10) || 15000,
          blindLevelMinutes: 15,
        });
      }

      triggerHaptic('medium');
      setIsSuccess(true);

      // Обновляем список турниров в админке
      const activeCityId = selectedCity !== undefined ? selectedCity : selectedCityId;
      const targetCityId = activeCityId === null ? null : activeCityId;
      const targetClubId = targetCityId === null ? null : selectedClubId;
      await fetchAdminSchedule(targetCityId, targetClubId);

      setTimeout(() => {
        setEditingTournament(null);
        setActiveTab('admin-tournaments');
      }, 700);
    } catch (err: unknown) {
      console.error(editingTournament ? 'Ошибка обновления турнира:' : 'Ошибка создания турнира:', err);
      triggerHaptic('heavy');
      if (axios.isAxiosError(err)) {
        const data = err.response?.data as { message?: string; title?: string; errors?: Record<string, string[] | string> } | undefined;
        let serverMessage = data?.message || data?.title;
        if (data?.errors && typeof data.errors === 'object') {
          const firstKey = Object.keys(data.errors)[0];
          const firstError = Array.isArray(data.errors[firstKey]) ? data.errors[firstKey][0] : data.errors[firstKey];
          if (firstError) {
            serverMessage = serverMessage ? `${serverMessage}: ${firstError}` : String(firstError);
          }
        }
        if (!serverMessage && typeof err.response?.data === 'string' && !err.response.data.trim().startsWith('<')) {
          serverMessage = err.response.data;
        }
        setErrorMsg(serverMessage || err.message || (editingTournament ? 'Ошибка сервера при обновлении турнира.' : 'Ошибка сервера при создании турнира.'));
      } else if (err instanceof Error) {
        setErrorMsg(err.message);
      } else {
        setErrorMsg(editingTournament ? 'Не удалось обновить турнир. Проверьте соединение с сервером.' : 'Не удалось создать турнир. Проверьте соединение с сервером.');
      }
      setLoading(false);
    }
  };

  return (
    <div className="px-5 pb-28 animate-fade-in space-y-4">
      {/* Баннер режима редактирования */}
      {editingTournament && (
        <div className="p-3.5 rounded-2xl bg-amber-950/40 border border-amber-500/40 flex items-center justify-between gap-3 animate-fade-in shadow-lg">
          <div className="flex items-center gap-2.5 min-w-0">
            <Pencil className="w-4 h-4 text-[#c39a44] shrink-0" />
            <div className="min-w-0">
              <div className="text-xs font-bold text-amber-200">Режим редактирования</div>
              <div className="text-[11px] text-[#8fa89b] truncate">Турнир #{editingTournament.id}: «{editingTournament.title}»</div>
            </div>
          </div>
          <button
            type="button"
            onClick={() => {
              setEditingTournament(null);
              setActiveTab('admin-tournaments');
            }}
            className="px-3 py-1.5 rounded-xl bg-black/50 hover:bg-black/80 border border-white/10 text-xs font-semibold text-white/80 hover:text-white shrink-0 active:scale-95 transition-all flex items-center gap-1.5"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            <span>Отмена</span>
          </button>
        </div>
      )}

      {/* Баннер успешного создания/обновления */}
      {isSuccess && (
        <div className="p-4 rounded-2xl bg-[#0d2a1f] border border-[#34d399] text-[#a4c9b7] text-xs flex items-center gap-3 animate-fade-in">
          <CheckCircle2 className="w-5 h-5 text-[#34d399] shrink-0" />
          <div>
            <div className="font-bold text-white">
              {editingTournament ? 'Турнир успешно обновлен!' : 'Турнир успешно создан!'}
            </div>
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
              onChange={(e) => {
                setRegEnd(e.target.value);
                if (fieldErrors.regEnd) setFieldErrors(prev => ({ ...prev, regEnd: '' }));
              }}
              placeholder="18:45"
              className={`w-full py-3 px-4 rounded-2xl bg-black/60 border text-sm font-semibold text-white focus:outline-none transition-all ${
                fieldErrors.regEnd ? 'border-red-500 bg-red-950/20' : 'border-white/10 focus:border-[#c39a44]'
              }`}
            />
            {fieldErrors.regEnd && <p className="text-[10px] text-red-400 mt-1">{fieldErrors.regEnd}</p>}
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
            {loading
              ? (editingTournament ? 'Сохранение изменений...' : 'Создание турнира...')
              : isSuccess
              ? (editingTournament ? 'Успешно обновлено!' : 'Успешно создано!')
              : (editingTournament ? 'Сохранить изменения' : 'Опубликовать турнир')}
          </button>
        </div>
      </form>
    </div>
  );
};
