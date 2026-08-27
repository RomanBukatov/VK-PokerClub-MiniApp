import axios from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5052';

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
  const searchParams = window.location.search;
  if (searchParams && searchParams.length > 1 && searchParams.includes('vk_user_id')) {
    config.headers['X-VK-Sign'] = searchParams;
  } else {
    // Режим разработки: берем сохраненный test VkId из localStorage
    const savedVkId = localStorage.getItem('vk_test_user_id') || '123456789';
    config.headers['X-Test-Vk-Id'] = savedVkId;
  }

  return config;
}, (error) => {
  return Promise.reject(error);
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const message = error.response?.data?.message || error.message || 'Ошибка сетевого запроса';
    console.error('API Error:', message, error);
    return Promise.reject(error);
  }
);
