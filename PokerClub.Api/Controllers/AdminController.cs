using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PokerClub.Api.Filters;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Services;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IGoogleSheetsSyncService _syncService;
    private readonly ILeaderboardCacheResetToken? _cacheResetToken;
    private readonly IMemoryCache? _memoryCache;

    public AdminController(
        IGoogleSheetsSyncService syncService,
        ILeaderboardCacheResetToken? cacheResetToken = null,
        IMemoryCache? memoryCache = null)
    {
        _syncService = syncService;
        _cacheResetToken = cacheResetToken;
        _memoryCache = memoryCache;
    }

    [HttpPost("sync-sheets")]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<ActionResult<GoogleSheetsSyncResult>> SyncGoogleSheets(CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncFromGoogleSheetsAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        try
        {
            _cacheResetToken?.Reset();
            if (_memoryCache is MemoryCache memCache)
            {
                memCache.Clear();
            }
        }
        catch
        {
            // Не ломаем ответ при ошибке сброса кэша
        }

        return Ok(result);
    }
}
