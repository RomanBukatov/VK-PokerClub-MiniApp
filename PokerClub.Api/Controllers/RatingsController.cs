using Microsoft.AspNetCore.Mvc;
using PokerClub.Api.DTOs;
using PokerClub.Api.Filters;
using PokerClub.Domain.Interfaces;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RatingsController : ControllerBase
{
    private readonly IRatingService _ratingService;

    public RatingsController(IRatingService ratingService)
    {
        _ratingService = ratingService;
    }

    [HttpGet("leaderboard")]
    public async Task<ActionResult<List<LeaderboardEntryDto>>> GetLeaderboard(
        [FromQuery] int limit = 50,
        [FromQuery] string type = "season")
    {
        if (limit <= 0) limit = 50;
        if (limit > 100) limit = 100;

        var isSeason = !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase) && 
                       !string.Equals(type, "all-time", StringComparison.OrdinalIgnoreCase);

        var users = await _ratingService.GetLeaderboardAsync(limit, isSeason ? "season" : "all");
        
        var result = users.Select((u, index) => {
            var activePoints = isSeason ? u.SeasonRating : u.TotalRating;
            return new LeaderboardEntryDto(
                index + 1,
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

        return Ok(result);
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

        return Ok(new { Message = message });
    }
}