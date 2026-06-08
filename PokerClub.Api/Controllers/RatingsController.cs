using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 50)
    {
        var users = await _ratingService.GetLeaderboardAsync(limit);
        
        var result = users.Select((u, index) => new 
        {
            Rank = index + 1,
            u.Id,
            u.VkId,
            u.FirstName,
            u.LastName,
            u.AvatarUrl,
            u.TotalRating
        });

        return Ok(result);
    }

    [HttpPost("admin/assign-points")]
    public async Task<IActionResult> AssignPoints([FromBody] AssignPointsRequest request)
    {
        var (success, message) = await _ratingService.AssignPointsAndFinishTournamentAsync(
            request.TournamentId, 
            request.UserPoints);

        if (!success)
            return BadRequest(new { Message = message });

        return Ok(new { Message = message });
    }
}

public record AssignPointsRequest(int TournamentId, Dictionary<int, int> UserPoints);