import vkBridge from '@vkontakte/vk-bridge';
import type { VkUser } from '../types';

const MOCK_USER: VkUser = {
  id: 0,
  first_name: 'Гость',
  last_name: 'Клуба',
  photo_200: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=200&auto=format&fit=crop&q=80',
  isAdmin: false,
};

async function sendWithTimeout<T>(
  promise: Promise<T>,
  timeoutMs = 1500,
  errorMsg = 'VK Bridge timeout'
): Promise<T> {
  return Promise.race([
    promise,
    new Promise<T>((_, reject) =>
      setTimeout(() => reject(new Error(errorMsg)), timeoutMs)
    ),
  ]);
}

interface TelegramUser {
  id: number;
  first_name: string;
  last_name?: string;
  username?: string;
  photo_url?: string;
}

interface TelegramWebApp {
  ready?: () => void;
  expand?: () => void;
  setHeaderColor?: (color: string) => void;
  setBackgroundColor?: (color: string) => void;
  initData?: string;
  initDataUnsafe?: {
    user?: TelegramUser;
  };
  HapticFeedback?: {
    impactOccurred: (style: 'light' | 'medium' | 'heavy' | 'rigid' | 'soft') => void;
  };
}

declare global {
  interface Window {
    Telegram?: {
      WebApp?: TelegramWebApp;
    };
  }
}

export async function initVkBridge(): Promise<VkUser> {
  // 1. Проверяем запуск в Telegram Mini App
  const tgWebApp = typeof window !== 'undefined' ? window.Telegram?.WebApp : undefined;
  if (tgWebApp && (tgWebApp.initData || tgWebApp.initDataUnsafe?.user)) {
    try {
      tgWebApp.ready?.();
      tgWebApp.expand?.();
      tgWebApp.setHeaderColor?.('#01201a');
      tgWebApp.setBackgroundColor?.('#01201a');
    } catch {
      // Игнорируем в веб-версии
    }

    const tgUser = tgWebApp.initDataUnsafe?.user;
    if (tgUser?.id) {
      console.info('Запуск в Telegram Mini App. Пользователь:', tgUser.id, tgUser.first_name);
      localStorage.setItem('vk_test_user_id', tgUser.id.toString());
      localStorage.setItem('tg_user_id', tgUser.id.toString());

      return {
        id: tgUser.id,
        first_name: tgUser.first_name || 'Игрок',
        last_name: tgUser.last_name || '',
        photo_200: tgUser.photo_url,
        photo_100: tgUser.photo_url,
        isAdmin: false,
      };
    }
  }

  const isVkEnvironment = typeof window !== 'undefined' && 
    (window.location.search.includes('vk_user_id') || window.location.search.includes('vk_app_id'));

  // Если приложение открыто в обычном браузере (локально или на сервере без параметров запуска VK)
  if (!isVkEnvironment) {
    console.info('Запуск в обычном браузере (Гость Клуба).');
    const existingTestId = typeof window !== 'undefined' ? localStorage.getItem('vk_test_user_id') : null;
    if (!existingTestId && typeof window !== 'undefined') {
      localStorage.setItem('vk_test_user_id', MOCK_USER.id.toString());
    }
    return {
      ...MOCK_USER,
      id: existingTestId ? (Number(existingTestId) || 0) : 0,
    };
  }

  try {
    // 1. Инициализируем VK Mini App с таймаутом 1.5 сек
    await sendWithTimeout(
      vkBridge.send('VKWebAppInit'),
      1500,
      'Таймаут инициализации VKWebAppInit'
    );

    // 2. Настраиваем системный статус-бар и навигационную полосу
    try {
      await vkBridge.send('VKWebAppSetViewSettings', {
        status_bar_style: 'light',      // Белые иконки часов и батарейки
        action_bar_color: '#01201a',    // Изумрудный цвет нативного хедера ВК
        navigation_bar_color: '#01201a' // Изумрудный цвет нижней навигационной полосы Android
      });
    } catch (e) {
      console.warn('VKWebAppSetViewSettings не поддерживается в данном окружении', e);
    }

    // 3. Запрашиваем информацию о пользователе с таймаутом 1.5 сек
    const userInfo = await sendWithTimeout(
      vkBridge.send('VKWebAppGetUserInfo'),
      1500,
      'Таймаут получения данных пользователя VK'
    );
    
    if (userInfo?.id) {
      localStorage.setItem('vk_test_user_id', userInfo.id.toString());
    }

    return {
      id: userInfo.id,
      first_name: userInfo.first_name,
      last_name: userInfo.last_name,
      photo_200: userInfo.photo_200,
      photo_100: userInfo.photo_100,
      city: userInfo.city,
      isAdmin: false,
    };
  } catch (err) {
    console.warn('VK Bridge не ответил или запущен вне VK. Применен гостевой профиль (Гость Клуба).', err);
    const existingTestId = typeof window !== 'undefined' ? localStorage.getItem('vk_test_user_id') : null;
    if (!existingTestId && typeof window !== 'undefined') {
      localStorage.setItem('vk_test_user_id', MOCK_USER.id.toString());
    }
    return {
      ...MOCK_USER,
      id: existingTestId ? (Number(existingTestId) || 0) : 0,
    };
  }
}

export function triggerHaptic(style: 'light' | 'medium' | 'heavy' = 'medium') {
  try {
    const tgWebApp = typeof window !== 'undefined' ? window.Telegram?.WebApp : undefined;
    if (tgWebApp?.HapticFeedback) {
      tgWebApp.HapticFeedback.impactOccurred(style);
      return;
    }
    vkBridge.send('VKWebAppTapticImpactOccurred', { style });
  } catch {
    // Ignore in browser
  }
}

export const requestGroupMessagesPermission = async (groupId: number = 238367404) => {
  const targetGroupId = Math.abs(Number(groupId)) || 238367404;
  try {
    console.log('Вызов VKWebAppAllowMessagesFromGroup с group_id:', targetGroupId);
    const result = await vkBridge.send('VKWebAppAllowMessagesFromGroup', { group_id: targetGroupId });
    console.log('Результат VKWebAppAllowMessagesFromGroup:', result);
    return result;
  } catch (e) {
    console.warn('Пользователь отклонил запрос на сообщения или ошибка моста:', e);
    return null;
  }
};

export const openExternalUrl = (url: string) => {
  if (!url || typeof window === 'undefined') return;

  // 1. Telegram WebApp: нативное открытие внешней ссылки
  try {
    const tgWebApp = window.Telegram?.WebApp;
    if (tgWebApp && typeof (tgWebApp as unknown as { openLink?: (u: string) => void }).openLink === 'function') {
      (tgWebApp as unknown as { openLink: (u: string) => void }).openLink(url);
      return;
    }
  } catch (err) {
    console.warn('Telegram openLink failed:', err);
  }

  // 2. VK Bridge: отправка события VKWebAppOpenURL в клиент/контейнер VK
  try {
    const sendBridge = vkBridge.send as unknown as (method: string, props?: Record<string, unknown>) => Promise<unknown>;
    sendBridge('VKWebAppOpenURL', { url }).catch((err) => {
      console.warn('vkBridge VKWebAppOpenURL ignored/unsupported:', err);
    });
  } catch (err) {
    console.warn('vkBridge send threw:', err);
  }

  // 3. iOS Safari в iframe (например, m.vk.ru на iPhone)
  // На iOS в iframe системный блокировщик всплывающих окон Safari блокирует window.open.
  // Перенаправление window.top открывает Universal Link vk.me в приложении VK или в Safari!
  const isIframe = window.self !== window.top;
  const userAgent = typeof navigator !== 'undefined' ? (navigator.userAgent || '') : '';
  const isIOS = /iPad|iPhone|iPod/.test(userAgent) ||
    (typeof navigator !== 'undefined' && navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

  if (isIframe && isIOS) {
    try {
      if (window.top) {
        window.top.location.href = url;
        return;
      }
    } catch {
      // Игнорируем ошибку cross-origin доступа к top
    }
    window.location.href = url;
    return;
  }

  // 4. Обычные браузеры (ПК, Android): пробуем открыть новую вкладку
  let popup: Window | null = null;
  try {
    popup = window.open(url, '_blank', 'noopener,noreferrer');
  } catch {
    // Popup creation blocked
  }

  // Если окно заблокировано браузером (Safari на десктопе или блокировщик всплывающих окон)
  if (!popup || popup.closed || typeof popup.closed === 'undefined') {
    try {
      if (window.top && window.top !== window) {
        window.top.location.href = url;
        return;
      }
    } catch {
      // Игнорируем ошибку cross-origin доступа к top
    }
    window.location.href = url;
  }
};

