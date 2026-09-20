using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PokerClub.Api.Filters;
using PokerClub.Api.Models;
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
    private readonly IConfiguration? _configuration;
    private readonly IOptions<VkOptions>? _vkOptions;
    private readonly IHttpClientFactory? _httpClientFactory;

    public AdminController(
        IGoogleSheetsSyncService syncService,
        ILeaderboardCacheResetToken? cacheResetToken = null,
        IMemoryCache? memoryCache = null,
        IConfiguration? configuration = null,
        IOptions<VkOptions>? vkOptions = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        _syncService = syncService;
        _cacheResetToken = cacheResetToken;
        _memoryCache = memoryCache;
        _configuration = configuration;
        _vkOptions = vkOptions;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("test-vk-message")]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<IActionResult> TestVkMessage(
        [FromQuery] string targetVkId = "308885723",
        [FromQuery] string? message = null)
    {
        var communityToken = _vkOptions?.Value?.CommunityToken
            ?? _configuration?["VkOptions:CommunityToken"]
            ?? _configuration?["VK_COMMUNITY_TOKEN"]
            ?? Environment.GetEnvironmentVariable("VK_COMMUNITY_TOKEN");

        if (string.IsNullOrWhiteSpace(communityToken))
        {
            return BadRequest(new 
            { 
                Success = false, 
                Message = "VK CommunityToken не настроен ни в конфигурации, ни в переменных окружения." 
            });
        }

        var cleanVkId = (targetVkId ?? string.Empty).Replace("id", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (!long.TryParse(cleanVkId, out var numericVkUserId) || numericVkUserId <= 0)
        {
            return BadRequest(new 
            { 
                Success = false, 
                Message = $"Некорректный targetVkId: '{targetVkId}'. Требуется числовой идентификатор VK." 
            });
        }

        var msgText = !string.IsNullOrWhiteSpace(message) 
            ? message.Trim() 
            : "♠️ Тест связи с клубом Monte Carlo";

        var parameters = new Dictionary<string, string>
        {
            ["user_id"] = numericVkUserId.ToString(),
            ["random_id"] = Random.Shared.Next(1, int.MaxValue).ToString(),
            ["peer_id"] = numericVkUserId.ToString(),
            ["message"] = msgText,
            ["access_token"] = communityToken.Trim(),
            ["v"] = "5.199"
        };

        var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        using var content = new FormUrlEncodedContent(parameters);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await client.PostAsync("https://api.vk.com/method/messages.send", content, cts.Token);
        var responseBody = await response.Content.ReadAsStringAsync();

        return Ok(new
        {
            Success = response.IsSuccessStatusCode && !responseBody.Contains("\"error\""),
            StatusCode = (int)response.StatusCode,
            TargetVkId = numericVkUserId,
            RawResponse = responseBody
        });
    }

    [HttpPost("sync-sheets")]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<ActionResult<GoogleSheetsSyncResult>> SyncSheets()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = await _syncService.SyncFromGoogleSheetsAsync(cts.Token);
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

    [NonAction]
    public Task<ActionResult<GoogleSheetsSyncResult>> SyncGoogleSheets(CancellationToken cancellationToken = default)
        => SyncSheets();
}
