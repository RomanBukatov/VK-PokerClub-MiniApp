# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проектов для кеширования NuGet-пакетов
COPY PokerClub.Domain/PokerClub.Domain.csproj PokerClub.Domain/
COPY PokerClub.Infrastructure/PokerClub.Infrastructure.csproj PokerClub.Infrastructure/
COPY PokerClub.Api/PokerClub.Api.csproj PokerClub.Api/

RUN dotnet restore PokerClub.Api/PokerClub.Api.csproj

# Копируем остальной исходный код
COPY PokerClub.Domain/ PokerClub.Domain/
COPY PokerClub.Infrastructure/ PokerClub.Infrastructure/
COPY PokerClub.Api/ PokerClub.Api/

WORKDIR /src/PokerClub.Api
RUN dotnet publish PokerClub.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 5052
ENV ASPNETCORE_URLS=http://+:5052

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "PokerClub.Api.dll"]
