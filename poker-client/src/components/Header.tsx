import React from 'react';
import { useUserStore } from '../store/useUserStore';
import { triggerHaptic } from '../utils/vkBridge';
import logoSvg from '../assets/logo.svg';

interface HeaderProps {
  title?: string;
  subtitle?: string;
}

export const Header: React.FC<HeaderProps> = ({ title, subtitle }) => {
  const { activeTab, isAdmin, setIsAdmin, setIsCityModalOpen, selectedCityName } = useUserStore();

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
    <header className="px-5 pt-4 pb-3 flex items-center justify-between safe-top">
      {/* Левая часть: Заголовок и подзаголовок */}
      <div className="flex flex-col">
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-extrabold text-white tracking-tight leading-none">
            {headerInfo.title}
          </h1>

          {/* Город с кликом для смены */}
          <button
            onClick={() => { triggerHaptic('light'); setIsCityModalOpen(true); }}
            className="text-[11px] text-[#7d9b8c] hover:text-[#c39a44] transition-colors flex items-center gap-0.5 mt-0.5"
            title="Сменить город"
          >
            <span>• {selectedCityName}</span>
          </button>
        </div>

        <p className="text-xs text-[#8fa89b] mt-1 font-normal">
          {headerInfo.subtitle}
        </p>
      </div>

      {/* Правая часть: Оригинальный векторный логотип Monte Carlo из Figma */}
      <div className="flex items-center gap-3">
        <button
          onClick={() => {
            triggerHaptic('light');
            setIsAdmin(!isAdmin);
          }}
          className="opacity-95 hover:opacity-100 transition-opacity active:scale-95 flex flex-col items-end"
          title="Переключить режим Администратора"
        >
          <img src={logoSvg} alt="Monte Carlo" className="h-8 object-contain" />
          {isAdmin && (
            <span className="text-[9px] font-bold text-[#c39a44] tracking-widest uppercase mt-0.5">
              ADMIN
            </span>
          )}
        </button>
      </div>
    </header>
  );
};
