using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PokerClub.Api.DTOs;
using PokerClub.Api.Filters;
using PokerClub.Domain.Interfaces;
using PokerClub.Infrastructure.Services;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RatingsController : ControllerBase
{
    public const int LeaderboardCacheDurationSeconds = 60;
    private readonly IRatingService _ratingService;
    private readonly IMemoryCache? _memoryCache;
    private readonly ILeaderboardCacheResetToken? _cacheResetToken;

    public RatingsController(
        IRatingService ratingService,
        IMemoryCache? memoryCache = null,
        ILeaderboardCacheResetToken? cacheResetToken = null)
    {
        _ratingService = ratingService;
        _memoryCache = memoryCache;
        _cacheResetToken = cacheResetToken;
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<LeaderboardResponseDto>> GetLeaderboard(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] string type = "season")
    {
        if (limit <= 0) limit = 50;
        if (limit > 100) limit = 100;
        if (offset < 0) offset = 0;

        var isSeason = !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) && 
                       !string.Equals(type, "all-time", StringComparison.OrdinalIgnoreCase);
        var normType = isSeason ? "season" : "all";

        var cacheKey = $"leaderboard_{normType}_{limit}_{offset}";

        if (_memoryCache != null && _memoryCache.TryGetValue(cacheKey, out LeaderboardResponseDto? cached) && cached != null)
        {
            return Ok(cached);
        }

        var (users, totalCount) = await _ratingService.GetLeaderboardAsync(limit, offset, normType);
        
        var items = users.Select((u, index) => {
            var activePoints = isSeason ? u.SeasonRating : u.TotalRating;
            return new LeaderboardEntryDto(
                offset + index + 1,
                u.Id,
                u.VkId,
                u.FirstName,
                u.LastName,
                u.AvatarUrl,
                activePoints,
                u.SeasonRating,
                activePoints
            );
        }).ToList();

        var response = new LeaderboardResponseDto(items, totalCount, limit, offset);

        if (_memoryCache != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(LeaderboardCacheDurationSeconds));

            if (_cacheResetToken != null)
            {
                cacheOptions.AddExpirationToken(_cacheResetToken.GetExpirationToken());
            }

            _memoryCache.Set(cacheKey, response, cacheOptions);
        }

        return Ok(response);
    }

    [HttpPost("admin/assign-points")]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<IActionResult> AssignPoints([FromBody] AssignPointsRequest request)
    {
        if (request.UserPoints == null || request.UserPoints.Count == 0)
            return BadRequest(new { Message = "Список очков пользователей не может быть пустым." });

        if (request.UserPoints.Values.Any(p => p < 0 || p > 100000))
            return BadRequest(new { Message = "Количество очков должно быть от 0 до 100 000." });

        var (success, message) = await _ratingService.AssignPointsAndFinishTournamentAsync(
            request.TournamentId, 
            request.UserPoints);

        if (!success)
            return BadRequest(new { Message = message });

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

        return Ok(new { Message = message });
    }
}