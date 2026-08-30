using Microsoft.AspNetCore.Mvc;
using PokerClub.Api.DTOs;
using PokerClub.Api.Extensions;
using PokerClub.Api.Filters;
using PokerClub.Domain.Enums;
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
    public async Task<ActionResult<List<TournamentScheduleDto>>> GetSchedule(
        [FromQuery] int? cityId, 
        [FromQuery] int? clubId,
        [FromQuery] bool includeFinished = false)
    {
        var schedule = await _tournamentService.GetScheduleAsync(cityId, clubId, includeFinished);
        var currentVkId = HttpContext.GetVkUserId();

        var result = schedule.Select(t => new TournamentScheduleDto(
            t.Id,
            t.Title,
            t.Format,
            t.BuyIn,
            t.Description,
            t.MaxSeats,
            t.StartTime,
            t.Status,
            t.ClubId,
            t.Club?.Name,
            t.Club?.City?.Name,
            t.Registrations.Count(r => r.Status == RegStatus.Active || r.Status == RegStatus.Played),
            !string.IsNullOrWhiteSpace(currentVkId) && 
            t.Registrations.Any(r => r.User?.VkId == currentVkId && (r.Status == RegStatus.Active || r.Status == RegStatus.Played))
        )).ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TournamentDetailDto>> GetTournament(int id)
    {
        var t = await _tournamentService.GetTournamentByIdAsync(id);
        if (t == null)
            return NotFound(new { Message = "Турнир не найден." });

        var currentVkId = HttpContext.GetVkUserId();
        var relevantRegistrations = t.Registrations
            .Where(r => r.Status == RegStatus.Active || r.Status == RegStatus.Played)
            .OrderBy(r => r.CreatedAt)
            .ToList();

        var participants = relevantRegistrations.Select(r => new RegisteredPlayerDto(
            r.UserId,
            r.User?.VkId ?? string.Empty,
            r.User?.FirstName,
            r.User?.LastName,
            r.User?.AvatarUrl,
            r.User?.TotalRating ?? 0,
            r.CreatedAt,
            r.PointsEarned
        )).ToList();

        var result = new TournamentDetailDto(
            t.Id,
            t.Title,
            t.Format,
            t.BuyIn,
            t.Description,
            t.MaxSeats,
            t.StartTime,
            t.Status,
            t.ClubId,
            t.Club?.Name,
            t.Club?.City?.Name,
            t.Club?.Address,
            relevantRegistrations.Count,
            !string.IsNullOrWhiteSpace(currentVkId) && 
            relevantRegistrations.Any(r => r.User?.VkId == currentVkId),
            participants
        );

        return Ok(result);
    }

    [HttpGet("my")]
    [VkAuthorize]
    public async Task<ActionResult<List<TournamentScheduleDto>>> GetMyTournaments()
    {
        var vkId = HttpContext.GetVkUserId();
        if (string.IsNullOrWhiteSpace(vkId))
            return Unauthorized(new { Message = "Не авторизован через VK." });

        var tournaments = await _tournamentService.GetUserTournamentsAsync(vkId);

        var result = tournaments.Select(t => new TournamentScheduleDto(
            t.Id,
            t.Title,
            t.Format,
            t.BuyIn,
            t.Description,
            t.MaxSeats,
            t.StartTime,
            t.Status,
            t.ClubId,
            t.Club?.Name,
            t.Club?.City?.Name,
            t.Registrations.Count(r => r.Status == RegStatus.Active),
            t.Registrations.Any(r => r.User?.VkId == vkId && r.Status == RegStatus.Active)
        )).ToList();

        return Ok(result);
    }

    [HttpPost("register")]
    [VkAuthorize]
    public async Task<IActionResult> Register([FromBody] RegisterPlayerRequest request)
    {
        var vkId = HttpContext.GetVkUserId() ?? request.VkId;
        if (string.IsNullOrWhiteSpace(vkId))
            return BadRequest(new { Message = "VK ID пользователя не определен." });

        var (success, message) = await _tournamentService.RegisterPlayerAsync(request.TournamentId, vkId);
        
        if (!success)
            return BadRequest(new { Message = message });

        return Ok(new { Message = message });
    }

    [HttpPost("unregister")]
    [VkAuthorize]
    public async Task<IActionResult> Unregister([FromBody] CancelRegistrationRequest request)
    {
        var vkId = HttpContext.GetVkUserId() ?? request.VkId;
        if (string.IsNullOrWhiteSpace(vkId))
            return BadRequest(new { Message = "VK ID пользователя не определен." });

        var (success, message) = await _tournamentService.CancelRegistrationAsync(request.TournamentId, vkId);
        
        if (!success)
            return BadRequest(new { Message = message });

        return Ok(new { Message = message });
    }

    [HttpPost]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<ActionResult<TournamentScheduleDto>> CreateTournament([FromBody] CreateTournamentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { Message = "Название турнира обязательно для заполнения." });

        if (request.Title.Trim().Length < 3)
            return BadRequest(new { Message = "Название турнира должно содержать не менее 3 символов." });

        if (request.MaxSeats <= 0 || request.MaxSeats > 1000)
            return BadRequest(new { Message = "Количество мест должно быть в диапазоне от 1 до 1 000." });

        if (request.BuyIn < 0)
            return BadRequest(new { Message = "Бай-ин не может быть отрицательным." });

        var (success, tournament, message) = await _tournamentService.CreateTournamentAsync(
            request.ClubId,
            request.Title,
            request.Format,
            request.BuyIn,
            request.MaxSeats,
            request.StartTime,
            request.Description
        );

        if (!success || tournament == null)
            return BadRequest(new { Message = message });

        var result = new TournamentScheduleDto(
            tournament.Id,
            tournament.Title,
            tournament.Format,
            tournament.BuyIn,
            tournament.Description,
            tournament.MaxSeats,
            tournament.StartTime,
            tournament.Status,
            tournament.ClubId,
            tournament.Club?.Name,
            tournament.Club?.City?.Name,
            0,
            false
        );

        return CreatedAtAction(nameof(GetTournament), new { id = tournament.Id }, result);
    }
}