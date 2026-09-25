using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokerClub.Domain.Interfaces;

namespace PokerClub.Infrastructure.Services;

/// <summary>
/// Фоновый воркер для регулярной синхронизации данных из Google Sheets каждые 5 минут.
/// При успешной синхронизации принудительно сбрасывает In-Memory кэш лидерборда.
/// Любые сетевые ошибки логируются и не роняют сервер.
/// </summary>
public class GoogleSheetsBackgroundSyncService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GoogleSheetsBackgroundSyncService> _logger;
    private readonly ILeaderboardCacheResetToken? _cacheResetToken;
    private readonly IMemoryCache? _memoryCache;
    private readonly TimeSpan _period;

    public GoogleSheetsBackgroundSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<GoogleSheetsBackgroundSyncService> logger,
        ILeaderboardCacheResetToken? cacheResetToken = null,
        IMemoryCache? memoryCache = null,
        TimeSpan? period = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cacheResetToken = cacheResetToken;
        _memoryCache = memoryCache;
        _period = period ?? TimeSpan.FromMinutes(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GoogleSheetsBackgroundSyncService запущен с интервалом {Interval} мин.", _period.TotalMinutes);

        // Немедленный разовый запуск синхронизации на старте приложения (без 5-минутного ожидания)
        try
        {
            _logger.LogInformation("Первоначальный запуск синхронизации Google Sheets на старте...");
            using var initialScope = _scopeFactory.CreateScope();
            var initialSyncService = initialScope.ServiceProvider.GetRequiredService<IGoogleSheetsSyncService>();
            var initialResult = await initialSyncService.SyncAsync(stoppingToken);

            if (initialResult.Success)
            {
                _logger.LogInformation(
                    "Первоначальная синхронизация Google Sheets успешно завершена: обработано {Processed}, обновлено {Updated}, создано {Created}.",
                    initialResult.TotalProcessed, initialResult.UpdatedCount, initialResult.CreatedCount);

                ResetLeaderboardCache();
            }
            else
            {
                _logger.LogWarning("Первоначальная синхронизация Google Sheets завершилась с ошибкой: {Message}", initialResult.Message);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении первоначальной синхронизации Google Sheets на старте.");
        }

        using var timer = new PeriodicTimer(_period);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    break;
                }

                _logger.LogInformation("Запуск фоновой периодической синхронизации Google Sheets...");

                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IGoogleSheetsSyncService>();

                var result = await syncService.SyncAsync(stoppingToken);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Фоновая синхронизация Google Sheets успешно завершена: обработано {Processed}, обновлено {Updated}, создано {Created}.",
                        result.TotalProcessed, result.UpdatedCount, result.CreatedCount);

                    ResetLeaderboardCache();
                }
                else
                {
                    _logger.LogWarning("Фоновая синхронизация Google Sheets завершилась с ошибкой: {Message}", result.Message);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("GoogleSheetsBackgroundSyncService останавливается по запросу отмены.");
                break;
            }
            catch (Exception ex)
            {
                // Любые сетевые или иные ошибки логируются и не роняют сервер
                _logger.LogError(ex, "Сетевая ошибка или сбой при выполнении фоновой синхронизации Google Sheets.");
            }
        }
    }

    /// <summary>
    /// Принудительный сброс In-Memory кэша лидерборда
    /// </summary>
    public void ResetLeaderboardCache()
    {
        try
        {
            _cacheResetToken?.Reset();

            if (_memoryCache is MemoryCache memCache)
            {
                memCache.Clear();
            }

            _logger.LogInformation("In-Memory кэш лидерборда успешно сброшен.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сбросе In-Memory кэша лидерборда.");
        }
    }
}
