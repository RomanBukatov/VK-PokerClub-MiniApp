import React from 'react';
import { MapPin, ChevronDown, Shield, User } from 'lucide-react';
import { useUserStore } from '../store/useUserStore';
import { triggerHaptic } from '../utils/vkBridge';
import logoSvg from '../assets/logo.svg';

interface HeaderProps {
  title?: string;
  subtitle?: string;
}

export const Header: React.FC<HeaderProps> = ({ title, subtitle }) => {
  const { activeTab, isAdmin, setIsAdmin, setIsCityModalOpen, selectedCityName, vkUser } = useUserStore();

  const getHeaderInfo = () => {
    if (title && subtitle) return { title, subtitle };

    switch (activeTab) {
      case 'schedule':
        return { title: 'Турниры', subtitle: 'Ближайшие игры в нашем клубе' };
      case 'leaderboard':
        return { title: 'Рейтинг', subtitle: 'Сезон: лето 2026' };
      case 'profile':
        return { title: 'Профиль', subtitle: 'Ваши игры и результаты' };
      case 'admin-tournaments':
        return { title: 'Управление', subtitle: 'Прошедшие игры' };
      case 'admin-create':
        return { title: 'Создание турнира', subtitle: 'Новое событие в расписании' };
      default:
        return { title: 'Турниры', subtitle: 'Ближайшие игры в нашем клубе' };
    }
  };

  const headerInfo = getHeaderInfo();

  return (
    <header className="px-5 pt-4 pb-3 flex flex-col gap-2.5 safe-top">
      {/* Верхняя строка: Логотип и переключатель режима / города */}
      <div className="flex items-center justify-between">
        {/* Кнопка выбора города */}
        <button
          type="button"
          onClick={() => {
            triggerHaptic('light');
            setIsCityModalOpen(true);
          }}
          className="bg-[#0a231b] border border-[#1e533f] hover:border-[#c39a44] text-[#a4c9b7] hover:text-white px-3 py-1.5 rounded-full text-xs font-semibold flex items-center gap-1.5 shadow-sm active:scale-95 transition-all"
          title="Сменить город"
        >
          <MapPin className="w-3.5 h-3.5 text-[#c39a44] shrink-0" />
          <span className="truncate max-w-[130px]">{selectedCityName}</span>
          <ChevronDown className="w-3.5 h-3.5 text-[#7d9b8c] shrink-0" />
        </button>

        {/* Правая часть: Админ переключатель и логотип */}
        <div className="flex items-center gap-2.5">
          {vkUser?.isAdmin && (
            <button
              type="button"
              onClick={() => {
                triggerHaptic('light');
                setIsAdmin(!isAdmin);
              }}
              className={`px-2.5 py-1 rounded-full text-xs font-semibold border transition-all active:scale-95 flex items-center gap-1.5 shadow-sm ${
                isAdmin
                  ? 'bg-[#c39a44]/20 border-[#c39a44] text-[#e5c06e]'
                  : 'bg-[#0a231b] border-[#1e533f] text-[#8fa89b] hover:text-white'
              }`}
              title="Переключить режим"
            >
              {isAdmin ? (
                <>
                  <Shield className="w-3 h-3 text-[#c39a44]" />
                  <span>Администратор</span>
                </>
              ) : (
                <>
                  <User className="w-3 h-3 text-[#8fa89b]" />
                  <span>Режим игрока</span>
                </>
              )}
            </button>
          )}

          <img src={logoSvg} alt="Monte Carlo" className="h-7 object-contain" />
        </div>
      </div>

      {/* Нижняя строка: Заголовок и подзаголовок текущего раздела */}
      <div>
        <h1 className="text-2xl font-extrabold text-white tracking-tight leading-tight">
          {headerInfo.title}
        </h1>
        <p className="text-xs text-[#8fa89b] font-normal mt-0.5">
          {headerInfo.subtitle}
        </p>
      </div>
    </header>
  );
};
