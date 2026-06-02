# ♠️ VK Poker Club - Mini App (MVP)

A lightweight and fast backend & frontend solution for managing a local Poker Club via VK Mini Apps. Built with clean architecture principles.

## 🚀 Tech Stack

**Backend:**
- C# / .NET 10 (ASP.NET Core Web API)
- Entity Framework Core 9/10
- PostgreSQL
- Clean Architecture (Api, Domain, Infrastructure)

**Frontend:**
- React 18 + TypeScript
- Vite
- Tailwind CSS
- VK UI (VK Bridge)

## 📋 Features (MVP)
- **Multi-Branch Support:** Manage multiple cities and local clubs.
- **Tournament Schedule:** View upcoming tournaments, formats, and buy-ins.
- **Registrations:** One-click registration for players via VK ID.
- **Rating System:** Automated leaderboard generation based on tournament results.
- **Admin Panel:** Fast tournament resolution and point distribution.

## 🏗️ Architecture & Database
The project follows Domain-Driven Design principles with a strict separation of concerns. 
- Rating calculations are optimized using denormalized `TotalRating` fields and background cached aggregations to keep cloud server costs near zero.

## 🛠️ Local Setup

1. **Clone the repo:**
   ```bash
   git clone https://github.com/RomanBukatov/VK-PokerClub-MiniApp.git
   ```
2. **Setup Database:**
   Update the connection string in `PokerClub.Api/appsettings.Development.json` and run migrations:
   ```bash
   dotnet ef database update --project PokerClub.Infrastructure --startup-project PokerClub.Api
   ```
3. **Run Backend:**
   ```bash
   cd PokerClub.Api
   dotnet run
   ```
4. **Run Frontend:**
   ```bash
   cd poker-client
   npm install
   npm run dev
   ```

---
*Developed for production use inside VKontakte Mini Apps ecosystem.*
```
