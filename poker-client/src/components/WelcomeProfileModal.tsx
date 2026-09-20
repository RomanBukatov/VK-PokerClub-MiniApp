import React, { useState } from 'react';
import { User, Phone, CreditCard, Sparkles, X, AlertCircle } from 'lucide-react';
import { useUserStore } from '../store/useUserStore';
import { triggerHaptic } from '../utils/vkBridge';
import { formatPhoneNumber } from '../utils/formatters';

export const WelcomeProfileModal: React.FC = () => {
  const { isProfileModalOpen } = useUserStore();

  if (!isProfileModalOpen) return null;

  return <ProfileModalContent key={isProfileModalOpen ? 'open' : 'closed'} />;
};

const ProfileModalContent: React.FC = () => {
  const { setIsProfileModalOpen, vkUser, profile, updateProfile } = useUserStore();

  const [nickname, setNickname] = useState(() => {
    if (profile?.nickname) return profile.nickname;
    if (vkUser?.first_name) {
      return vkUser.first_name.replace(/[^a-zA-Z0-9а-яА-ЯёЁ_]/g, '').slice(0, 16);
    }
    return '';
  });

  const [fullName, setFullName] = useState(() => {
    const isPlaceholder = (val: string) => {
      const lower = val.trim().toLowerCase();
      return (
        lower === 'гость клуба' ||
        lower === 'клуба гость' ||
        lower === 'гость' ||
        lower === 'игрок vk' ||
        lower === 'игрок' ||
        lower.startsWith('игрок #') ||
        /^игрок\s*\d+$/i.test(lower)
      );
    };

    if (profile?.fullName && !isPlaceholder(profile.fullName)) {
      return profile.fullName;
    }
    if (profile?.lastName || profile?.firstName) {
      const combined = `${profile.lastName || ''} ${profile.firstName || ''}`.trim();
      if (combined && !isPlaceholder(combined)) {
        return combined;
      }
    }
    if (vkUser?.last_name || vkUser?.first_name) {
      const combined = `${vkUser.last_name || ''} ${vkUser.first_name || ''}`.trim();
      if (combined && !isPlaceholder(combined)) {
        return combined;
      }
    }
    return '';
  });

  const [phoneNumber, setPhoneNumber] = useState(() => profile?.phoneNumber || '');
  const [clubCardId, setClubCardId] = useState(() => profile?.clubCardId || '');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Обработка Backspace перед нецифровыми символами-разделителями
  const handlePhoneKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace') {
      const input = e.currentTarget;
      const { selectionStart, selectionEnd, value } = input;
      // Если курсор стоит после разделителя ('-', ')', ' ', '(') и нет диапазона выделения
      if (selectionStart !== null && selectionStart === selectionEnd && selectionStart > 0) {
        const charBefore = value[selectionStart - 1];
        if (/\D/.test(charBefore)) {
          e.preventDefault();
          let digits = value.replace(/\D/g, '');
          if (digits.startsWith('7') || digits.startsWith('8')) {
            digits = digits.slice(1);
          }
          if (digits.length > 0) {
            setPhoneNumber(formatPhoneNumber(digits.slice(0, -1)));
          } else {
            setPhoneNumber('');
          }
        }
      }
    }
  };

  // Форматирование телефона по маске +7 (XXX) XXX-XX-XX
  const handlePhoneChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = e.target.value;

    // Разрешаем полностью стереть телефон до пустого поля
    if (!val || val === '+' || val === '+7' || val === '+7 ' || val === '+7 (') {
      setPhoneNumber('');
      return;
    }

    let prevDigits = phoneNumber.replace(/\D/g, '');
    if (prevDigits.startsWith('7') || prevDigits.startsWith('8')) {
      prevDigits = prevDigits.slice(1);
    }

    let nextDigits = val.replace(/\D/g, '');
    if (nextDigits.startsWith('7') || nextDigits.startsWith('8')) {
      nextDigits = nextDigits.slice(1);
    }

    // Если длина строки уменьшилась (нажатие Backspace/Delete),
    // но количество цифр осталось прежним (пользователь стер только дефис/скобку/пробел),
    // принудительно удаляем последнюю цифру из пула
    if (val.length < phoneNumber.length && nextDigits.length === prevDigits.length && nextDigits.length > 0) {
      nextDigits = nextDigits.slice(0, -1);
    }

    setPhoneNumber(formatPhoneNumber(nextDigits));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const trimmedNick = nickname.trim();
    if (trimmedNick.length < 3 || trimmedNick.length > 20) {
      setError('Игровой никнейм должен содержать от 3 до 20 символов.');
      triggerHaptic('heavy');
      return;
    }

    if (!/^[a-zA-Z0-9а-яА-ЯёЁ_]+$/.test(trimmedNick)) {
      setError('Никнейм может содержать только буквы, цифры и знак подчеркивания.');
      triggerHaptic('heavy');
      return;
    }

    const trimmedFullName = fullName.trim();
    if (!trimmedFullName || trimmedFullName.length < 2) {
      setError('Введите ваше реальное ФИО (Имя и Фамилию).');
      triggerHaptic('heavy');
      return;
    }

    const lowerFullName = trimmedFullName.toLowerCase();
    if (
      lowerFullName === 'гость клуба' ||
      lowerFullName === 'клуба гость' ||
      lowerFullName === 'гость' ||
      lowerFullName === 'игрок vk' ||
      lowerFullName === 'игрок' ||
      lowerFullName.startsWith('игрок #') ||
      /^игрок\s*\d+$/i.test(trimmedFullName)
    ) {
      setError('Пожалуйста, укажите ваши реальные Имя и Фамилию.');
      triggerHaptic('heavy');
      return;
    }

    const trimmedPhone = phoneNumber.trim();
    const phoneDigits = trimmedPhone.replace(/\D/g, '');
    if (!trimmedPhone || phoneDigits.length < 11) {
      setError('Введите полный номер телефона в формате +7 (___) ___-__-__.');
      triggerHaptic('heavy');
      return;
    }

    setIsSubmitting(true);
    triggerHaptic('medium');

    try {
      const nameParts = trimmedFullName.split(/\s+/);
      const firstName = nameParts.length > 1 ? nameParts.slice(1).join(' ') : nameParts[0];
      const lastName = nameParts.length > 1 ? nameParts[0] : undefined;

      await updateProfile({
        nickname: trimmedNick,
        fullName: trimmedFullName,
        firstName,
        lastName,
        phoneNumber: trimmedPhone || undefined,
        clubCardId: clubCardId.trim() || undefined,
      });
      triggerHaptic('light');
    } catch (err: unknown) {
      const msg = err && typeof err === 'object' && 'response' in err
        ? (err as { response?: { data?: { message?: string } } }).response?.data?.message
        : 'Не удалось сохранить профиль. Попробуйте снова.';
      setError(msg || 'Не удалось сохранить профиль.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-md animate-fade-in select-none">
      <div className="w-full max-w-md bg-[#0d281e] border border-[#c39a44]/30 rounded-3xl p-6 shadow-2xl shadow-black/80 flex flex-col max-h-[92vh] overflow-y-auto text-white">
        {/* Заголовок */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-[#c39a44]/30 to-[#c39a44]/10 border border-[#c39a44]/40 flex items-center justify-center text-[#c39a44]">
              <Sparkles className="w-6 h-6" />
            </div>
            <div>
              <h2 className="text-lg font-black tracking-tight text-white leading-tight">
                Добро пожаловать в клуб
              </h2>
              <p className="text-xs text-[#8fa89b]">
                Заполните анкету для турнирной сетки
              </p>
            </div>
          </div>

          {/* Кнопка закрытия, если профиль уже частично сохранен */}
          {profile?.nickname && (
            <button
              type="button"
              onClick={() => setIsProfileModalOpen(false)}
              className="p-2 rounded-xl bg-white/5 hover:bg-white/10 text-white/60 hover:text-white transition-all"
            >
              <X className="w-5 h-5" />
            </button>
          )}
        </div>

        {error && (
          <div className="mb-4 p-3 rounded-2xl bg-red-950/80 border border-red-500/40 text-red-300 text-xs flex items-center gap-2 animate-fade-in">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Игровой никнейм */}
          <div>
            <label className="block text-[11px] font-bold uppercase tracking-wider text-[#8fa89b] mb-1.5">
              Игровой никнейм <span className="text-[#c39a44]">*</span>
            </label>
            <div className="relative">
              <input
                type="text"
                required
                maxLength={20}
                placeholder="AceKing_77"
                value={nickname}
                onChange={(e) => setNickname(e.target.value)}
                className="w-full bg-black/40 border border-white/10 focus:border-[#c39a44] rounded-2xl py-3 px-4 text-sm font-semibold text-white placeholder-white/20 focus:outline-none transition-all"
              />
              <span className="absolute right-3.5 top-3.5 text-[10px] font-bold text-white/30">
                {nickname.length}/20
              </span>
            </div>
            <p className="text-[10px] text-[#606a66] mt-1">
              3-20 символов (буквы, цифры, подчеркивание)
            </p>
          </div>

          {/* Ваше имя (ФИО) */}
          <div>
            <label className="block text-[11px] font-bold uppercase tracking-wider text-[#8fa89b] mb-1.5">
              Ваше реальное имя (ФИО) <span className="text-[#c39a44]">*</span>
            </label>
            <div className="relative">
              <User className="w-4 h-4 text-white/30 absolute left-4 top-3.5" />
              <input
                type="text"
                required
                maxLength={80}
                placeholder="Иванов Иван"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                className={`w-full bg-black/40 border ${
                  error && (error.includes('ФИО') || error.includes('Фамилию'))
                    ? 'border-red-500/80 focus:border-red-400'
                    : 'border-white/10 focus:border-[#c39a44]'
                } rounded-2xl py-3 pl-11 pr-4 text-sm font-semibold text-white placeholder-white/20 focus:outline-none transition-all`}
              />
            </div>
            <p className="text-[10px] text-[#606a66] mt-1">
              Реальные Имя и Фамилия для турнирной сетки и клубного рейтинга
            </p>
          </div>

          {/* Телефон с маской */}
          <div>
            <label className="block text-[11px] font-bold uppercase tracking-wider text-[#8fa89b] mb-1.5">
              Номер телефона <span className="text-[#c39a44]">*</span>
            </label>
            <div className="relative">
              <Phone className="w-4 h-4 text-white/30 absolute left-4 top-3.5" />
              <input
                type="tel"
                required
                placeholder="+7 (___) ___-__-__"
                value={phoneNumber}
                onChange={handlePhoneChange}
                onKeyDown={handlePhoneKeyDown}
                className={`w-full bg-black/40 border ${
                  error && error.includes('телефон') ? 'border-red-500/80 focus:border-red-400' : 'border-white/10 focus:border-[#c39a44]'
                } rounded-2xl py-3 pl-11 pr-4 text-sm font-semibold text-white placeholder-white/20 focus:outline-none transition-all`}
              />
            </div>
            <p className="text-[10px] text-[#606a66] mt-1">
              Для подтверждения брони мест за столом
            </p>
          </div>

          {/* Клубный ID (опционально) */}
          <div>
            <label className="block text-[11px] font-bold uppercase tracking-wider text-[#8fa89b] mb-1.5">
              Клубный ID / Номер карты <span className="text-white/40 text-[10px] lowercase">(опционально)</span>
            </label>
            <div className="relative">
              <CreditCard className="w-4 h-4 text-white/30 absolute left-4 top-3.5" />
              <input
                type="text"
                maxLength={20}
                placeholder="Например: 1266"
                value={clubCardId}
                onChange={(e) => setClubCardId(e.target.value)}
                className={`w-full bg-black/40 border ${
                  error && (error.includes('карт') || error.includes('карты')) ? 'border-red-500/80 focus:border-red-400' : 'border-white/10 focus:border-[#c39a44]'
                } rounded-2xl py-3 pl-11 pr-4 text-sm font-semibold text-white placeholder-white/20 focus:outline-none transition-all`}
              />
            </div>
            <p className="text-[10px] text-[#606a66] mt-1">
              Моментально привяжет ваши очки из Google Sheets «МК РЕЙТИНГ»
            </p>
          </div>

          {/* Кнопка отправки */}
          <div className="pt-2">
            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full py-3.5 px-5 rounded-2xl font-extrabold text-sm bg-gradient-to-r from-[#d8af56] to-[#b38833] text-black shadow-lg shadow-[#c39a44]/30 hover:brightness-105 active:scale-[0.98] transition-all disabled:opacity-50"
            >
              {isSubmitting ? 'Сохранение...' : 'Сохранить и войти в игру'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
