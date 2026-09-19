import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL ?? '';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 10000,
});

// Получение и кэширование параметров запуска VK (для надежной работы внутри iframe VK)
export const getVkLaunchParams = (): string => {
  if (typeof window === 'undefined') return '';

  // 1. Проверяем текущий window.location.search
  const search = window.location.search || '';
  if (search && search.length > 1 && (search.includes('vk_user_id') || search.includes('sign='))) {
    try {
      sessionStorage.setItem('vk_launch_search', search);
    } catch {
      // ignore
    }
    return search;
  }

  // 2. Проверяем сохраненные параметры запуска сессии
  try {
    const cached = sessionStorage.getItem('vk_launch_search');
    if (cached) return cached;
  } catch {
    // ignore
  }

  // 3. Проверяем hash, если параметры были переданы после #
  const hash = window.location.hash || '';
  if (hash && (hash.includes('vk_user_id') || hash.includes('sign='))) {
    const queryPart = hash.includes('?') ? hash.split('?')[1] : hash.replace(/^#/, '');
    const formatted = queryPart.startsWith('?') ? queryPart : `?${queryPart}`;
    try {
      sessionStorage.setItem('vk_launch_search', formatted);
    } catch {
      // ignore
    }
    return formatted;
  }

  return search;
};

// Перехватчик для автоматической отправки параметров запуска VK
apiClient.interceptors.request.use((config) => {
  // Увеличиваем таймаут для POST-запросов (например, регистрация на турнир) до 25 секунд
  if (config.method?.toUpperCase() === 'POST' && (!config.timeout || config.timeout === 10000)) {
    config.timeout = 25000;
  }

  // 1. Проверяем URL search params от VK (с кэшированием в сессии на случай SPA-навигации)
  const searchParams = getVkLaunchParams();
  if (searchParams && searchParams.length > 1 && (searchParams.includes('vk_user_id') || searchParams.includes('sign='))) {
    config.headers['X-VK-Sign'] = searchParams;
  }

  // 2. Проверяем Telegram Mini App
  const tgWebApp = typeof window !== 'undefined' ? window.Telegram?.WebApp : undefined;
  if (tgWebApp?.initData) {
    config.headers['Authorization'] = `tma ${tgWebApp.initData}`;
    if (tgWebApp.initDataUnsafe?.user?.id) {
      config.headers['X-Telegram-Id'] = tgWebApp.initDataUnsafe.user.id.toString();
      config.headers['X-Telegram-User'] = JSON.stringify(tgWebApp.initDataUnsafe.user);
    }
  }
  const savedTgId = typeof window !== 'undefined' ? localStorage.getItem('tg_user_id') : null;
  if (savedTgId && !config.headers['X-Telegram-Id']) {
    config.headers['X-Telegram-Id'] = savedTgId;
  }

  // Всегда передаем тестовый VK ID при автономном/демо запуске,
  // чтобы даже при отсутствии реальной подписи VK или при смене роли эндпоинты с [VkAuthorize] работали корректно.
  const savedVkId = (typeof window !== 'undefined' ? localStorage.getItem('vk_test_user_id') : null) || '0';
  config.headers['X-Test-Vk-Id'] = savedVkId;

  // Передаем статус админа: права отправляются ТОЛЬКО при явном savedRole === 'true' (P0-1, P0-2, P2-7)
  const savedRole = typeof window !== 'undefined' ? localStorage.getItem('poker_is_admin') : null;
  config.headers['X-Is-Admin'] = savedRole === 'true' ? 'true' : 'false';

  return config;
}, (error) => {
  return Promise.reject(error);
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const rawUrl = error.config
      ? `${error.config.baseURL || ''}${error.config.url || ''}`
      : 'неизвестный URL';
    const fullUrl =
      typeof window !== 'undefined' && rawUrl.startsWith('/')
        ? `${window.location.origin}${rawUrl}`
        : rawUrl;

    const isNetworkError =
      error.message === 'Network Error' ||
      error.code === 'ERR_NETWORK' ||
      error.code === 'ECONNABORTED' ||
      !error.response;

    if (isNetworkError) {
      console.error(`[Network Error] Сбой сетевого запроса к URL: ${fullUrl}`, {
        url: fullUrl,
        method: error.config?.method?.toUpperCase(),
        code: error.code,
        message: error.message,
      });
    }

    const message =
      error.response?.data?.message ||
      error.response?.data?.title ||
      error.message ||
      'Ошибка сетевого запроса';
    console.error(`API Error [${fullUrl}]:`, message, error);
    return Promise.reject(error);
  }
);
