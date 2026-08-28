# ♠️ VK Poker Club — Mini App

Современное решение для управления локальным покерным клубом через экосистему **VK Mini Apps**. Построено по принципам Clean Architecture (.NET 10 + PostgreSQL) и быстрого SPA-клиента (React 19 + TypeScript + Tailwind CSS + Bun).

---

## 🚀 Технологический стек

### **Backend:**
- **.NET 10 (ASP.NET Core Web API)**
- **Entity Framework Core 10 (Npgsql)**
- **PostgreSQL 16**
- **Clean Architecture** (`PokerClub.Api`, `PokerClub.Domain`, `PokerClub.Infrastructure`)
- **VK Launch Params Validation** (HMAC-SHA256 валидация подписи параметров запуска VK)
- **Scalar API Reference & OpenAPI** (интерактивная документация)

### **Frontend:**
- **React 19 + TypeScript**
- **Bun** (пакетный менеджер и среда выполнения)
- **Vite 8 + Tailwind CSS**
- **VK Bridge** (`@vkontakte/vk-bridge`) с поддержкой Haptic feedback и Standalone Mock-режимом
- **Zustand** (глобальный стейт-менеджмент)
- **Axios** (с интерцептором передачи подписи `X-VK-Sign`)
- **Lucide Icons**

### **DevOps & Контейнеризация:**
- **Docker & Docker Compose** (многоэтапные сборки для .NET и Bun/Nginx)
- **Nginx Alpine** (раздача SPA-статики, gzip-сжатие, реверс-прокси API и Scalar Docs)

---

## 📋 Основной функционал

- **🏙️ Мульти-филиальность:** Выбор городов и локальных клубов с удобным селектором в шапке.
- **📅 Расписание турниров:** Просмотр предстоящих и завершенных игр, форматов, бай-инов, стартовых стеков и структуры блайндов.
- **🎟️ Запись в один клик:** Быстрая регистрация и отмена записи через VK ID с контролем лимита мест и проверкой статусов (`Announced`, `RegistrationOpen`, `Finished`).
- **👥 Список участников:** Просмотр зарегистрированных игроков на странице турнира с аватарами и рейтингом.
- **🏆 Таблица лидеров:** Автоматический расчет лидерборда по итогам турниров с пьедесталом топ-3 и персональным рангом игрока.
- **👤 Профиль игрока:** Личная статистика, текущий рейтинг и история сыгранных турниров с начисленными очками.
- **👑 Панель администратора:**
  - Создание новых турниров с гибкой настройкой параметров.
  - Управление статусами и распределение очков по итогам игр.
  - Быстрое переключение между режимом администратора и режимом игрока для тестирования.

---

## 🐳 Быстрый запуск в Docker (Рекомендуется)

Все компоненты приложения (PostgreSQL, .NET 10 API, Bun/React Nginx) поднимаются одной командой:

```bash
docker compose up --build -d
```

### Доступные сервисы:
- 🌐 **Клиент (VK Mini App)**: [http://localhost:3000](http://localhost:3000)
- 📖 **Документация API (Scalar)**: [http://localhost:3000/scalar/v1](http://localhost:3000/scalar/v1) или [http://localhost:5052/scalar/v1](http://localhost:5052/scalar/v1)
- 🐘 **PostgreSQL**: `localhost:5439` (пользователь `poker_admin`, БД `poker_club_db`)

---

## 🛠️ Локальная разработка без Docker

### 1. Требования
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Bun](https://bun.sh/) (>= 1.1)
- [PostgreSQL](https://www.postgresql.org/) (>= 15)

### 2. Настройка базы данных
Укажите строку подключения в `PokerClub.Api/appsettings.Development.json` и примените миграции:
```bash
dotnet ef database update --project PokerClub.Infrastructure --startup-project PokerClub.Api
```
*(При первом запуске бэкенда миграции и сидирование тестовых данных выполняются автоматически)*

### 3. Запуск Backend API
```bash
cd PokerClub.Api
dotnet run
```
API запустится на `http://localhost:5052`.

### 4. Запуск Frontend Client
```bash
cd poker-client
bun install
bun run dev
```
Клиент запустится на `http://localhost:5173`.

---

## 🔒 Переменные окружения (.env)

В корне проекта:
```env
DB_USER=poker_admin
DB_PASSWORD=super_secret_poker_pass_2026
DB_NAME=poker_club_db
DB_PORT=5439
```

В `poker-client/.env`:
```env
VITE_API_URL=http://localhost:5052
```

---

*Разработано для использования в экосистеме VK Mini Apps.*
