using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PokerClub.Api.DTOs;
using PokerClub.Api.Extensions;
using PokerClub.Api.Filters;
using PokerClub.Domain.Entities;
using PokerClub.Domain.Enums;
using PokerClub.Domain.Interfaces;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _tournamentService;
    private readonly ILogger<TournamentsController>? _logger;

    public TournamentsController(ITournamentService tournamentService, ILogger<TournamentsController>? logger = null)
    {
        _tournamentService = tournamentService;
        _logger = logger;
    }

    [HttpGet("schedule")]
    public async Task<ActionResult<List<TournamentScheduleDto>>> GetSchedule(
        [FromQuery] int? cityId, 
        [FromQuery] int? clubId,
        [FromQuery] bool includeFinished = false)
    {
        try
        {
            var schedule = await _tournamentService.GetScheduleAsync(cityId, clubId, includeFinished);

            // 1. Если для конкретного города/клуба турниры не найдены (например, город отфильтрован или турниры в другом клубе/городе) —
            // подтягиваем турниры из всех клубов и городов, чтобы расписание не пропадало у пользователя
            if (schedule.Count == 0 && (cityId.HasValue && cityId.Value > 0 || clubId.HasValue && clubId.Value > 0))
            {
                // Сначала пробуем тот же город, но любой клуб (если был передан clubId)
                if (clubId.HasValue && clubId.Value > 0 && cityId.HasValue && cityId.Value > 0)
                {
                    var cityAnyClubSchedule = await _tournamentService.GetScheduleAsync(cityId, null, includeFinished);
                    if (cityAnyClubSchedule.Count > 0)
                    {
                        schedule = cityAnyClubSchedule;
                    }
                }

                // Если все еще пусто — подтягиваем турниры из всех городов и клубов
                if (schedule.Count == 0)
                {
                    var allCitiesSchedule = await _tournamentService.GetScheduleAsync(null, null, includeFinished);
                    if (allCitiesSchedule.Count > 0)
                    {
                        schedule = allCitiesSchedule;
                    }
                }
            }

            // 2. Если активных турниров нет, но есть прошедшие/завершенные — показываем их, чтобы расписание не выглядело пустым
            if (schedule.Count == 0 && !includeFinished)
            {
                var finishedSchedule = await _tournamentService.GetScheduleAsync(cityId, clubId, includeFinished: true);
                if (finishedSchedule.Count == 0 && (cityId.HasValue && cityId.Value > 0 || clubId.HasValue && clubId.Value > 0))
                {
                    finishedSchedule = await _tournamentService.GetScheduleAsync(null, null, includeFinished: true);
                }

                if (finishedSchedule.Count > 0)
                {
                    schedule = finishedSchedule;
                }
            }

            var list = schedule ?? new List<Tournament>();

            var currentVkId = HttpContext.GetVkUserId();

            var result = list.Select(t => new TournamentScheduleDto(
                t.Id,
                t.Title ?? "Турнир",
                t.Format,
                t.BuyIn,
                t.Description,
                t.MaxSeats,
                t.StartTime,
                t.Status,
                t.ClubId,
                t.Club?.Name,
                t.Club?.City?.Name,
                t.Registrations != null ? t.Registrations.Count(r => r.Status == RegStatus.Active || r.Status == RegStatus.Played) : 0,
                !string.IsNullOrWhiteSpace(currentVkId) && 
                t.Registrations != null && t.Registrations.Any(r => r.User?.VkId == currentVkId && (r.Status == RegStatus.Active || r.Status == RegStatus.Played)),
                t.RegistrationEnd,
                t.Club?.Address,
                t.StartingStack > 0 ? t.StartingStack : 10000
            )).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при формировании расписания турниров.");
            return Ok(new List<TournamentScheduleDto>());
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TournamentDetailDto>> GetTournament(int id)
    {
        try
        {
            var t = await _tournamentService.GetTournamentByIdAsync(id);
            if (t == null)
                return NotFound(new { Message = "Турнир не найден." });

            var currentVkId = HttpContext.GetVkUserId();
            var relevantRegistrations = (t.Registrations != null ? t.Registrations.Where(r => r.Status == RegStatus.Active || r.Status == RegStatus.Played) : Enumerable.Empty<Registration>())
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
                t.Title ?? "Турнир",
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
                participants,
                t.RegistrationEnd,
                t.StartingStack > 0 ? t.StartingStack : 10000
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при получении турнира {TournamentId}", id);
            return NotFound(new { Message = "Турнир не найден или временно недоступен." });
        }
    }

    [HttpGet("my")]
    [VkAuthorize]
    public async Task<ActionResult<List<TournamentScheduleDto>>> GetMyTournaments()
    {
        var vkId = HttpContext.GetVkUserId();
        if (string.IsNullOrWhiteSpace(vkId))
            return Unauthorized(new { Message = "Не авторизован через VK." });

        try
        {
            var tournaments = await _tournamentService.GetUserTournamentsAsync(vkId);

            var tournamentsList = tournaments ?? new List<Tournament>();

            var result = tournamentsList.Select(t => new TournamentScheduleDto(
                t.Id,
                t.Title ?? "Турнир",
                t.Format,
                t.BuyIn,
                t.Description,
                t.MaxSeats,
                t.StartTime,
                t.Status,
                t.ClubId,
                t.Club?.Name,
                t.Club?.City?.Name,
                t.Registrations != null ? t.Registrations.Count(r => r.Status == RegStatus.Active || r.Status == RegStatus.Played) : 0,
                t.Registrations != null && t.Registrations.Any(r => r.User != null && r.User.VkId == vkId && (r.Status == RegStatus.Active || r.Status == RegStatus.Played)),
                t.RegistrationEnd,
                t.Club?.Address,
                t.StartingStack > 0 ? t.StartingStack : 10000
            )).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при получении моих турниров пользователя {VkId}", vkId);
            return Ok(new List<TournamentScheduleDto>());
        }
    }

    [HttpPost("register")]
    [VkAuthorize]
    public async Task<IActionResult> Register([FromBody] RegisterPlayerRequest request)
    {
        try
        {
            var vkId = HttpContext.GetVkUserId() ?? request.VkId;
            if (string.IsNullOrWhiteSpace(vkId))
                return BadRequest(new { Message = "VK ID пользователя не определен." });

            var (success, message) = await _tournamentService.RegisterPlayerAsync(
                request.TournamentId, 
                vkId,
                request.FirstName,
                request.LastName,
                request.AvatarUrl);
            
            if (!success)
                return BadRequest(new { Message = message });

            return Ok(new { Message = message });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при регистрации пользователя на турнир {TournamentId}", request.TournamentId);
            return BadRequest(new { Message = "Ошибка при записи на турнир." });
        }
    }

    [HttpPost("unregister")]
    [VkAuthorize]
    public async Task<IActionResult> Unregister([FromBody] CancelRegistrationRequest request)
    {
        try
        {
            var vkId = HttpContext.GetVkUserId() ?? request.VkId;
            if (string.IsNullOrWhiteSpace(vkId))
                return BadRequest(new { Message = "VK ID пользователя не определен." });

            var (success, message) = await _tournamentService.CancelRegistrationAsync(request.TournamentId, vkId);
            
            if (!success)
                return BadRequest(new { Message = message });

            return Ok(new { Message = message });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при отмене записи на турнир {TournamentId}", request.TournamentId);
            return BadRequest(new { Message = "Ошибка при отмене записи на турнир." });
        }
    }

    [HttpPost]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<ActionResult<TournamentScheduleDto>> CreateTournament([FromBody] CreateTournamentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { Message = "Название турнира обязательно для заполнения." });

            if (request.Title.Trim().Length < 3)
                return BadRequest(new { Message = "Название турнира должно содержать не менее 3 символов." });

            if (request.MaxSeats <= 0 || request.MaxSeats > 1000)
                return BadRequest(new { Message = "Количество мест должно быть в диапазоне от 1 до 1 000." });

            if (request.BuyIn < 0)
                return BadRequest(new { Message = "Бай-ин не может быть отрицательным." });

            if (request.StartTime == default || request.StartTime.Year < 2020 || request.StartTime.Year > 2100)
                return BadRequest(new { Message = "Укажите корректную дату и время начала турнира." });

            if ((request.StartingStack.HasValue && request.StartingStack.Value <= 0) ||
                (request.StartingChips.HasValue && request.StartingChips.Value <= 0))
                return BadRequest(new { Message = "Стартовый стек должен быть больше 0." });

            var startingStack = request.StartingStack ?? request.StartingChips ?? 10000;
            var (success, tournament, message) = await _tournamentService.CreateTournamentAsync(
                request.ClubId,
                request.Title,
                request.Format,
                request.BuyIn,
                request.MaxSeats,
                request.StartTime,
                request.Description,
                request.CityId,
                request.Address,
                request.RegistrationEnd,
                startingStack
            );

            if (!success || tournament == null)
                return BadRequest(new { Message = message });

            var result = new TournamentScheduleDto(
                tournament.Id,
                tournament.Title ?? "Турнир",
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
                false,
                tournament.RegistrationEnd,
                tournament.Club?.Address,
                tournament.StartingStack > 0 ? tournament.StartingStack : 10000
            );

            return CreatedAtAction(nameof(GetTournament), new { id = tournament.Id }, result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при создании турнира");
            return BadRequest(new { Message = "Ошибка при создании турнира." });
        }
    }

    [HttpPut("{id:int}")]
    [HttpPost("{id:int}/update")]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<ActionResult<TournamentDetailDto>> UpdateTournament(int id, [FromBody] UpdateTournamentRequest request)
    {
        try
        {
            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                    return BadRequest(new { Message = "Название турнира обязательно для заполнения." });

                if (request.Title.Trim().Length < 3)
                    return BadRequest(new { Message = "Название турнира должно содержать не менее 3 символов." });
            }

            if (request.MaxSeats.HasValue && (request.MaxSeats.Value <= 0 || request.MaxSeats.Value > 1000))
                return BadRequest(new { Message = "Количество мест должно быть в диапазоне от 1 до 1 000." });

            if (request.BuyIn.HasValue && request.BuyIn.Value < 0)
                return BadRequest(new { Message = "Бай-ин не может быть отрицательным." });

            if (request.StartTime.HasValue && (request.StartTime.Value == default || request.StartTime.Value.Year < 2020 || request.StartTime.Value.Year > 2100))
                return BadRequest(new { Message = "Укажите корректную дату и время начала турнира." });

            if ((request.StartingStack.HasValue && request.StartingStack.Value <= 0) ||
                (request.StartingChips.HasValue && request.StartingChips.Value <= 0))
                return BadRequest(new { Message = "Стартовый стек должен быть больше 0." });

            var startingStack = request.StartingStack ?? request.StartingChips;
            var (success, tournament, message) = await _tournamentService.UpdateTournamentAsync(
                id,
                request.ClubId,
                request.Title,
                request.Format,
                request.BuyIn,
                request.MaxSeats,
                request.StartTime,
                request.Description,
                request.CityId,
                request.Address,
                request.RegistrationEnd,
                request.Status,
                request.ClearRegistrationEnd,
                startingStack
            );

            if (!success || tournament == null)
            {
                if (message == "Турнир не найден.")
                    return NotFound(new { Message = message });
                return BadRequest(new { Message = message });
            }

            var currentVkId = HttpContext.GetVkUserId();
            var relevantRegistrations = (tournament.Registrations != null ? tournament.Registrations.Where(r => r.Status == RegStatus.Active || r.Status == RegStatus.Played) : Enumerable.Empty<Registration>())
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
                tournament.Id,
                tournament.Title ?? "Турнир",
                tournament.Format,
                tournament.BuyIn,
                tournament.Description,
                tournament.MaxSeats,
                tournament.StartTime,
                tournament.Status,
                tournament.ClubId,
                tournament.Club?.Name,
                tournament.Club?.City?.Name,
                tournament.Club?.Address,
                relevantRegistrations.Count,
                !string.IsNullOrWhiteSpace(currentVkId) && 
                relevantRegistrations.Any(r => r.User?.VkId == currentVkId),
                participants,
                tournament.RegistrationEnd,
                tournament.StartingStack > 0 ? tournament.StartingStack : 10000
            );

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при обновлении турнира {TournamentId}", id);
            return BadRequest(new { Message = "Ошибка при обновлении турнира." });
        }
    }

    [HttpDelete("{id:int}")]
    [HttpPost("{id:int}/cancel")]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<IActionResult> DeleteTournament(int id)
    {
        try
        {
            var (success, message) = await _tournamentService.DeleteTournamentAsync(id);
            if (!success)
                return NotFound(new { Message = message });

            return Ok(new { Message = message });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при удалении турнира {TournamentId}", id);
            return BadRequest(new { Message = "Ошибка при удалении турнира." });
        }
    }
}