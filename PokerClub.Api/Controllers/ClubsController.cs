using Microsoft.AspNetCore.Mvc;
using PokerClub.Api.DTOs;
using PokerClub.Domain.Interfaces;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClubsController : ControllerBase
{
    private readonly IClubService _clubService;

    public ClubsController(IClubService clubService)
    {
        _clubService = clubService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ClubDto>>> GetClubs([FromQuery] int? cityId)
    {
        var clubs = await _clubService.GetClubsAsync(cityId);
        var result = clubs.Select(c => new ClubDto(
            c.Id,
            c.CityId,
            c.Name,
            c.Address,
            c.IsActive,
            c.City?.Name
        )).ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClubDto>> GetClubById(int id)
    {
        var club = await _clubService.GetClubByIdAsync(id);
        if (club == null)
            return NotFound(new { Message = "Клуб не найден." });

        var result = new ClubDto(
            club.Id,
            club.CityId,
            club.Name,
            club.Address,
            club.IsActive,
            club.City?.Name
        );

        return Ok(result);
    }
}
