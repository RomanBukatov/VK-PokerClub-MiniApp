import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import vkBridge from '@vkontakte/vk-bridge'
import './index.css'
import App from './App.tsx'

// Ранняя инициализация VK Mini App и настройка системного статус-бара
vkBridge.send('VKWebAppInit');
vkBridge.send('VKWebAppSetViewSettings', {
  status_bar_style: 'light',
  action_bar_color: '#01201a'
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
