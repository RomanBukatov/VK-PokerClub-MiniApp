using Microsoft.EntityFrameworkCore;
using PokerClub.Api.Models;
using PokerClub.Api.Services;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Data;
using PokerClub.Infrastructure.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Подключаем конфигурацию VK
builder.Services.Configure<VkOptions>(options =>
{
    builder.Configuration.GetSection(VkOptions.SectionName).Bind(options);

    // Поддержка чтения переменной окружения VK_ADMIN_IDS (через запятую, точку с запятой или пробел)
    var envAdminIds = builder.Configuration["VK_ADMIN_IDS"] 
        ?? builder.Configuration["VkOptions:AdminVkIds"]
        ?? Environment.GetEnvironmentVariable("VK_ADMIN_IDS");

    if (!string.IsNullOrWhiteSpace(envAdminIds))
    {
        var ids = envAdminIds.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        options.AdminVkIds = ids.Distinct().ToList();
    }

    var envAppId = builder.Configuration["VK_APP_ID"] ?? Environment.GetEnvironmentVariable("VK_APP_ID");
    if (!string.IsNullOrWhiteSpace(envAppId) && long.TryParse(envAppId, out var appId))
    {
        options.AppId = appId;
    }

    var envSecret = builder.Configuration["VK_PROTECTED_KEY"] ?? Environment.GetEnvironmentVariable("VK_PROTECTED_KEY");
    if (!string.IsNullOrWhiteSpace(envSecret))
    {
        options.ClientSecret = envSecret;
    }
});

// Подключаем PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Регистрируем сервисы бизнес-логики
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IRatingService, RatingService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IClubService, ClubService>();
builder.Services.AddScoped<IVkAuthValidator, VkAuthValidator>();
builder.Services.AddHttpClient<IGoogleSheetsSyncService, GoogleSheetsSyncService>(httpClient =>
{
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 5
});

// In-Memory кэширование и фоновые воркеры
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ILeaderboardCacheResetToken, LeaderboardCacheResetToken>();
builder.Services.AddHostedService<GoogleSheetsBackgroundSyncService>();

// Настройка CORS политики для локальной разработки и доменов ВКонтакте
builder.Services.AddCors(options =>
{
    options.AddPolicy("VkPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;

            try
            {
                var uri = new Uri(origin);
                // Локальная разработка (localhost / 127.0.0.1 на любых портах)
                if (uri.Host == "localhost" || uri.Host == "127.0.0.1") 
                    return true;

                // Домены ВКонтакте и VK Mini Apps
                if (uri.Host.EndsWith(".vk.com", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("vk.com", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith(".vk-apps.com", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("vk-apps.com", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

builder.Services.AddControllers();

// Включаем встроенную генерацию OpenAPI
builder.Services.AddOpenApi(); 

var app = builder.Build();

// ==========================================
// АВТОМАТИЧЕСКИЕ МИГРАЦИИ ПРИ СТАРТЕ
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        await DbInitializer.CleanFakeUsersAsync(context);
        await DbInitializer.CleanFakeTournamentsAsync(context);
        await DbInitializer.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ошибка при выполнении автоматических миграций или сида базы данных.");
    }
}

// Применяем политику CORS перед авторизацией
app.UseCors("VkPolicy");

// Включаем Scalar в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Генерирует OpenAPI / swagger.json
    app.MapScalarApiReference(options => 
    {
        options.Title = "Poker Club API";
        options.Theme = ScalarTheme.DeepSpace;
    });
}

app.UseAuthorization();
app.MapControllers();

app.Run();