import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL ?? '';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 10000,
});

// Перехватчик для автоматической отправки параметров запуска VK
apiClient.interceptors.request.use((config) => {
  // 1. Проверяем URL search params от VK
  const searchParams = typeof window !== 'undefined' ? window.location.search : '';
  if (searchParams && searchParams.length > 1 && searchParams.includes('vk_user_id')) {
    config.headers['X-VK-Sign'] = searchParams;
  }

  // Всегда передаем тестовый VK ID при автономном/демо запуске,
  // чтобы даже при отсутствии реальной подписи VK или при смене роли эндпоинты с [VkAuthorize] работали корректно.
  const savedVkId = (typeof window !== 'undefined' ? localStorage.getItem('vk_test_user_id') : null) || '123456789';
  config.headers['X-Test-Vk-Id'] = savedVkId;

  // Передаем статус админа для демо-режима и автономного тестирования.
  // Не удаляем заголовок, а передаем точное строковое значение ('true' или 'false').
  const savedRole = typeof window !== 'undefined' ? localStorage.getItem('poker_is_admin') : null;
  config.headers['X-Is-Admin'] = savedRole === 'false' ? 'false' : 'true';

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
