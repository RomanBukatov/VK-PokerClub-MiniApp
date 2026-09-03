import vkBridge from '@vkontakte/vk-bridge';
import type { VkUser } from '../types';

const MOCK_USER: VkUser = {
  id: 123456789,
  first_name: 'Станислав',
  last_name: 'Костров',
  photo_200: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=200&auto=format&fit=crop&q=80',
  isAdmin: true,
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

export async function initVkBridge(): Promise<VkUser> {
  const isVkEnvironment = typeof window !== 'undefined' && 
    (window.location.search.includes('vk_user_id') || window.location.search.includes('vk_app_id'));

  // Если приложение открыто локально в обычном браузере без параметров VK
  if (!isVkEnvironment && import.meta.env.DEV) {
    console.info('Запуск в режиме разработки (Standalone). Активирован тестовый профиль Станислава Кострова (Admin).');
    localStorage.setItem('vk_test_user_id', MOCK_USER.id.toString());
    return MOCK_USER;
  }

  try {
    // 1. Инициализируем VK Mini App с таймаутом 1.5 сек
    await sendWithTimeout(
      vkBridge.send('VKWebAppInit'),
      1500,
      'Таймаут инициализации VKWebAppInit'
    );

    // 2. Запрашиваем информацию о пользователе с таймаутом 1.5 сек
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
    console.warn('VK Bridge не ответил или вернул ошибку. Применен fallback.', err);
    if (!isVkEnvironment && import.meta.env.DEV) {
      localStorage.setItem('vk_test_user_id', MOCK_USER.id.toString());
      return MOCK_USER;
    }
    return {
      id: 1,
      first_name: 'Гость',
      last_name: 'Клуба',
      isAdmin: false,
    };
  }
}

export function triggerHaptic(style: 'light' | 'medium' | 'heavy' = 'medium') {
  try {
    vkBridge.send('VKWebAppTapticImpactOccurred', { style });
  } catch {
    // Ignore in browser
  }
}
