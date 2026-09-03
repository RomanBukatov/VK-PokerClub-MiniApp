import React from 'react';
import { useUserStore } from '../store/useUserStore';
import { triggerHaptic } from '../utils/vkBridge';
import type { TabType } from '../types';

export const TabBar: React.FC = () => {
  const { activeTab, setActiveTab, isAdmin } = useUserStore();

  const handleTabClick = (tab: TabType) => {
    triggerHaptic('light');
    setActiveTab(tab);
  };

  return (
    <div className="fixed bottom-4 left-0 right-0 z-40 flex justify-center px-4 pointer-events-none pb-[env(safe-area-inset-bottom)]">
      <nav className="pointer-events-auto flex items-center bg-[#06120d]/90 backdrop-blur-xl border border-white/10 p-1 rounded-full shadow-2xl shadow-black/80 max-w-xs w-full justify-between">
        {!isAdmin ? (
          <>
            <button
              onClick={() => handleTabClick('schedule')}
              className={`flex-1 py-2 rounded-full text-xs font-semibold text-center transition-all ${
                activeTab === 'schedule'
                  ? 'bg-[#192d23] text-white shadow-md'
                  : 'text-[#738f80] hover:text-white'
              }`}
            >
              Турниры
            </button>

            <button
              onClick={() => handleTabClick('leaderboard')}
              className={`flex-1 py-2 rounded-full text-xs font-semibold text-center transition-all ${
                activeTab === 'leaderboard'
                  ? 'bg-[#192d23] text-white shadow-md'
                  : 'text-[#738f80] hover:text-white'
              }`}
            >
              Рейтинг
            </button>

            <button
              onClick={() => handleTabClick('profile')}
              className={`flex-1 py-2 rounded-full text-xs font-semibold text-center transition-all ${
                activeTab === 'profile'
                  ? 'bg-[#192d23] text-white shadow-md'
                  : 'text-[#738f80] hover:text-white'
              }`}
            >
              Профиль
            </button>
          </>
        ) : (
          <>
            <button
              onClick={() => handleTabClick('admin-tournaments')}
              className={`flex-1 py-2 rounded-full text-xs font-semibold text-center transition-all ${
                activeTab === 'admin-tournaments'
                  ? 'bg-[#192d23] text-white shadow-md'
                  : 'text-[#738f80] hover:text-white'
              }`}
            >
              Управление
            </button>

            <button
              onClick={() => handleTabClick('leaderboard')}
              className={`flex-1 py-2 rounded-full text-xs font-semibold text-center transition-all ${
                activeTab === 'leaderboard'
                  ? 'bg-[#192d23] text-white shadow-md'
                  : 'text-[#738f80] hover:text-white'
              }`}
            >
              Рейтинг
            </button>

            <button
              onClick={() => handleTabClick('admin-create')}
              className={`flex-1 py-2 rounded-full text-xs font-semibold text-center transition-all ${
                activeTab === 'admin-create'
                  ? 'bg-[#192d23] text-white shadow-md'
                  : 'text-[#738f80] hover:text-white'
              }`}
            >
              Создать
            </button>
          </>
        )}
      </nav>
    </div>
  );
};
