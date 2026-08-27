import React, { useState } from 'react';
import { Plus } from 'lucide-react';
import { tournamentsApi } from '../../api/tournamentsApi';
import { useTournamentsStore } from '../../store/useTournamentsStore';
import { useUserStore } from '../../store/useUserStore';
import { triggerHaptic } from '../../utils/vkBridge';

export const AdminCreateTournamentPanel: React.FC = () => {
  const { fetchSchedule } = useTournamentsStore();
  const { selectedCityId, selectedClubId, setActiveTab } = useUserStore();

  const [title, setTitle] = useState('Freeroll Tournament');
  const [address, setAddress] = useState('Монастырская улица, 59, Пермь');
  const [date, setDate] = useState('10.08');
  const [time, setTime] = useState('19:00');
  const [format, setFormat] = useState('no limit');
  const [chips, setChips] = useState('10000');
  const [buyIn, setBuyIn] = useState('0');
  const [maxSeats, setMaxSeats] = useState('75');
  const [regEnd, setRegEnd] = useState('18:45');
  const [description, setDescription] = useState(
    'Бесплатный турнир для всех желающих.\n\n• Стартовый стек — 10 000 фишек\n• Блайнд-апы — 15 минут\n• Поздняя регистрация — 3 часа\n\nRe-Buy\n• 20 000 — 1000 ₽\n• Premium 40 000 — 1500 ₽\n\nAdd-on\n• 20 000 — 500 ₽\n• 40 000 — 1000 ₽\n• 60 000 — 1500 ₽ + 🍺'
  );
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    triggerHaptic('heavy');
    setLoading(true);
    setErrorMsg(null);

    try {
      const now = new Date();
      const currentYear = now.getFullYear();
      const [day, month] = date.split('.').map(Number);
      const [hour, minute] = time.split(':').map(Number);

      const startTime = new Date(currentYear, (month || 8) - 1, day || 10, hour || 19, minute || 0);

      await tournamentsApi.createTournament({
        title,
        clubId: selectedClubId || 1,
        startTime: startTime.toISOString(),
        buyIn: parseFloat(buyIn) || 0,
        maxSeats: parseInt(maxSeats, 10) || 75,
        description,
        format,
        startingChips: parseInt(chips, 10) || 10000,
        blindLevelMinutes: 15,
      });

      triggerHaptic('medium');
      await fetchSchedule(selectedCityId, selectedClubId);
      setActiveTab('admin-tournaments');
    } catch (err: unknown) {
      console.error('Ошибка создания турнира:', err);
      setErrorMsg('Ошибка при создании турнира.');
      setLoading(false);
    }
  };

  return (
    <div className="px-5 pb-28 animate-fade-in space-y-4">
      {errorMsg && (
        <div className="p-3 rounded-2xl bg-red-950/80 border border-red-500/40 text-red-400 text-xs">
          {errorMsg}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* Название турнира */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Название турнира
          </label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            className="w-full py-3 px-4 rounded-full bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Адрес */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Адрес
          </label>
          <input
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            className="w-full py-3 px-4 rounded-full bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Дата */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Дата
          </label>
          <input
            type="text"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            placeholder="10.08"
            className="w-full py-3 px-4 rounded-full bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Время */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Время
          </label>
          <input
            type="text"
            value={time}
            onChange={(e) => setTime(e.target.value)}
            placeholder="19:00"
            className="w-full py-3 px-4 rounded-full bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Параметры */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Параметры
          </label>
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <input
                type="text"
                value={format}
                onChange={(e) => setFormat(e.target.value)}
                placeholder="no limit"
                className="flex-1 py-2.5 px-4 rounded-full bg-black/60 border border-white/10 text-xs font-semibold text-white focus:outline-none focus:border-[#c39a44]"
              />
              <button type="button" className="p-2 rounded-full text-[#8fa89b] hover:text-white">
                <Plus className="w-5 h-5" />
              </button>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="text"
                value={`стартовый стек ${chips}`}
                onChange={(e) => setChips(e.target.value.replace(/\D/g, ''))}
                className="flex-1 py-2.5 px-4 rounded-full bg-black/60 border border-white/10 text-xs font-semibold text-white focus:outline-none focus:border-[#c39a44]"
              />
              <button type="button" className="p-2 rounded-full text-[#8fa89b] hover:text-white">
                <Plus className="w-5 h-5" />
              </button>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="text"
                value={`${buyIn} ₽`}
                onChange={(e) => setBuyIn(e.target.value.replace(/\D/g, ''))}
                className="flex-1 py-2.5 px-4 rounded-full bg-black/60 border border-white/10 text-xs font-semibold text-white focus:outline-none focus:border-[#c39a44]"
              />
              <button type="button" className="p-2 rounded-full text-[#8fa89b] hover:text-white">
                <Plus className="w-5 h-5" />
              </button>
            </div>
          </div>
        </div>

        {/* Количество мест */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Количество мест
          </label>
          <input
            type="number"
            value={maxSeats}
            onChange={(e) => setMaxSeats(e.target.value)}
            className="w-full py-3 px-4 rounded-full bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Конец регистрации */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            Конец регистрации
          </label>
          <input
            type="text"
            value={regEnd}
            onChange={(e) => setRegEnd(e.target.value)}
            placeholder="18:45"
            className="w-full py-3 px-4 rounded-full bg-black/60 border border-white/10 text-sm font-semibold text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* О турнире */}
        <div>
          <label className="block text-xs font-semibold text-[#8fa89b] mb-1.5">
            О турнире
          </label>
          <textarea
            rows={8}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="w-full p-4 rounded-3xl bg-black/60 border border-white/10 text-xs leading-relaxed text-white focus:outline-none focus:border-[#c39a44]"
          />
        </div>

        {/* Кнопка сохранить */}
        <div className="pt-2">
          <button
            type="submit"
            disabled={loading}
            className="w-full py-3.5 px-6 rounded-full bg-[#c39a44] text-white font-bold text-sm shadow-xl hover:brightness-105 active:scale-[0.98] transition-all disabled:opacity-50"
          >
            {loading ? 'Сохранение...' : 'Сохранить'}
          </button>
        </div>
      </form>
    </div>
  );
};
