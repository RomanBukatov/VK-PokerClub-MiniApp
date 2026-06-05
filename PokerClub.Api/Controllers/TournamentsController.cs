using Microsoft.AspNetCore.Mvc;
using PokerClub.Domain.Interfaces;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _tournamentService;

    public TournamentsController(ITournamentService tournamentService)
    {
        _tournamentService = tournamentService;
    }

    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule([FromQuery] int? cityId, [FromQuery] int? clubId)
    {
        var schedule = await _tournamentService.GetScheduleAsync(cityId, clubId);
        
        // Для MVP возвращаем анонимный объект, чтобы не светить лишние поля связей
        var result = schedule.Select(t => new 
        {
            t.Id,
            t.Title,
            t.Format,
            t.BuyIn,
            t.MaxSeats,
            t.StartTime,
            ClubName = t.Club?.Name,
            CityName = t.Club?.City?.Name,
            RegisteredCount = t.Registrations.Count
        });

        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterPlayerRequest request)
    {
        var (success, message) = await _tournamentService.RegisterPlayerAsync(request.TournamentId, request.VkId);
        
        if (!success)
            return BadRequest(new { Message = message });

        return Ok(new { Message = message });
    }
}

// Простейший DTO для запроса (рекорд экономит нам код)
public record RegisterPlayerRequest(int TournamentId, string VkId);