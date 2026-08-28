import React from 'react';
import { useUserStore } from '../store/useUserStore';
import { initVkBridge, triggerHaptic } from '../utils/vkBridge';
import { CURRENT_BRANDING } from '../config/branding';

export const WelcomePanel: React.FC = () => {
  const { setUser } = useUserStore();

  const handleLogin = async () => {
    triggerHaptic('medium');
    const user = await initVkBridge();
    if (user) {
      setUser(user);
    }
  };

  return (
    <div className="min-h-screen bg-[#01201a] relative flex flex-col justify-between p-6 overflow-hidden select-none">
      {/* 3D золотая фишка в левой верхней части */}
      <img
        src={CURRENT_BRANDING.assets.chipGoldImg}
        alt=""
        className="absolute -top-10 -left-12 w-48 h-48 object-contain pointer-events-none opacity-90 filter drop-shadow-2xl"
      />

      {/* Игральные карты в нижней части */}
      <img
        src={CURRENT_BRANDING.assets.cardsBgImg}
        alt=""
        className="absolute -bottom-16 -right-10 w-80 h-96 object-contain pointer-events-none opacity-80 filter drop-shadow-2xl"
      />

      {/* Верхний логотип по центру */}
      <div className="pt-12 flex justify-center z-10">
        <img src={CURRENT_BRANDING.assets.logoSvg} alt={CURRENT_BRANDING.clubName} className="h-14 object-contain" />
      </div>

      {/* Центральный блок текстов */}
      <div className="my-auto z-10 text-center px-2 py-8">
        <h1 className="text-3xl font-extrabold text-white leading-tight tracking-tight mb-3">
          Ваше место<br />за покерным столом
        </h1>
        <p className="text-sm text-[#8fa89b] max-w-xs mx-auto leading-relaxed">
          Турниры, записи и рейтинг игроков в одном приложении
        </p>
      </div>

      {/* Нижняя золотая кнопка */}
      <div className="pb-8 z-10">
        <button
          onClick={handleLogin}
          className="w-full py-4 px-6 rounded-full bg-[#c39a44] text-white font-bold text-base shadow-xl shadow-black/60 hover:brightness-105 active:scale-[0.98] transition-all"
        >
          Продолжить через VK
        </button>
      </div>
    </div>
  );
};
