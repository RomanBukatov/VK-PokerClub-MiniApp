import { useEffect, useState } from 'react';
import { useUserStore } from './store/useUserStore';
import { initVkBridge } from './utils/vkBridge';
import { Header } from './components/Header';
import { TabBar } from './components/TabBar';
import { CitySelectModal } from './components/CitySelectModal';
import { WelcomePanel } from './panels/WelcomePanel';
import { SchedulePanel } from './panels/SchedulePanel';
import { LeaderboardPanel } from './panels/LeaderboardPanel';
import { ProfilePanel } from './panels/ProfilePanel';
import { TournamentDetailModal } from './panels/TournamentDetailModal';
import { LegalModal } from './components/LegalModal';
import { WelcomeProfileModal } from './components/WelcomeProfileModal';
import { AdminTournamentsPanel } from './panels/admin/AdminTournamentsPanel';
import { AdminCreateTournamentPanel } from './panels/admin/AdminCreateTournamentPanel';
import { CURRENT_BRANDING } from './config/branding';

export function App() {
  const { isAuthenticated, activeTab, setUser } = useUserStore();
  const [isInitializing, setIsInitializing] = useState(true);

  useEffect(() => {
    let finished = false;

    // Аварийный таймаут на 3 секунды: если VKWebAppInit или запрос профиля
    // не ответили вовремя на медленном мобильном интернете, принудительно снимаем экран загрузки
    // и пускаем пользователя в расписание как гостя.
    const emergencyTimer = setTimeout(() => {
      if (!finished) {
        console.warn('Аварийный таймаут 3с сработал: принудительно пускаем в расписание как гостя');
        finished = true;
        const state = useUserStore.getState();
        if (!state.isAuthenticated) {
          useUserStore.setState({ isAuthenticated: true, activeTab: 'schedule' });
        }
        setIsInitializing(false);
      }
    }, 3000);

    initVkBridge()
      .then((user) => {
        if (user) {
          setUser(user);
        } else {
          const state = useUserStore.getState();
          if (!state.isAuthenticated) {
            useUserStore.setState({ isAuthenticated: true, activeTab: 'schedule' });
          }
        }
      })
      .catch((err) => {
        console.warn('Авторизация при старте:', err);
        const state = useUserStore.getState();
        if (!state.isAuthenticated) {
          useUserStore.setState({ isAuthenticated: true, activeTab: 'schedule' });
        }
      })
      .finally(() => {
        if (!finished) {
          finished = true;
          clearTimeout(emergencyTimer);
          setIsInitializing(false);
        }
      });

    return () => {
      finished = true;
      clearTimeout(emergencyTimer);
    };
  }, [setUser]);

  if (isInitializing) {
    return (
      <div className="max-w-md mx-auto min-h-screen bg-[#01201a] flex flex-col items-center justify-center p-6 text-center select-none">
        <img src={CURRENT_BRANDING.assets.logoSvg} alt={CURRENT_BRANDING.clubName} className="h-14 object-contain animate-pulse mb-3" />
        <div className="text-xs text-[#8fa89b]">Загрузка приложения...</div>
      </div>
    );
  }

  return (
    <div className="max-w-md mx-auto min-h-screen bg-[#01201a] text-white shadow-2xl relative flex flex-col selection:bg-[#c39a44] selection:text-white">
      {!isAuthenticated ? (
        <WelcomePanel />
      ) : (
        <>
          {/* Шапка приложения */}
          <Header />

          {/* Основное тело экранов */}
          <main className="flex-1">
            {activeTab === 'schedule' && <SchedulePanel />}
            {activeTab === 'leaderboard' && <LeaderboardPanel />}
            {activeTab === 'profile' && <ProfilePanel />}
            {activeTab === 'admin-tournaments' && <AdminTournamentsPanel />}
            {activeTab === 'admin-create' && <AdminCreateTournamentPanel />}
          </main>

          {/* Плавающий нижний таббар */}
          <TabBar />

          {/* Модальные окна */}
          <TournamentDetailModal />
          <CitySelectModal />
        </>
      )}

      {/* Модальные окна оферты и профиля (доступны при необходимости и на стартовом экране) */}
      <LegalModal />
      <WelcomeProfileModal />
    </div>
  );
}

export default App;
