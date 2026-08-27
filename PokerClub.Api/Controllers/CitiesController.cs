using Microsoft.AspNetCore.Mvc;
using PokerClub.Api.DTOs;
using PokerClub.Domain.Interfaces;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CitiesController : ControllerBase
{
    private readonly ICityService _cityService;

    public CitiesController(ICityService cityService)
    {
        _cityService = cityService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CityDto>>> GetCities()
    {
        var cities = await _cityService.GetActiveCitiesAsync();
        var result = cities.Select(c => new CityDto(
            c.Id,
            c.Name,
            c.Slug,
            c.IsActive,
            c.Clubs.Count(cl => cl.IsActive)
        )).ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CityDto>> GetCityById(int id)
    {
        var city = await _cityService.GetCityByIdAsync(id);
        if (city == null)
            return NotFound(new { Message = "Город не найден." });

        var result = new CityDto(
            city.Id,
            city.Name,
            city.Slug,
            city.IsActive,
            city.Clubs.Count(cl => cl.IsActive)
        );

        return Ok(result);
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<CityDto>> GetCityBySlug(string slug)
    {
        var city = await _cityService.GetCityBySlugAsync(slug);
        if (city == null)
            return NotFound(new { Message = "Город не найден." });

        var result = new CityDto(
            city.Id,
            city.Name,
            city.Slug,
            city.IsActive,
            city.Clubs.Count(cl => cl.IsActive)
        );

        return Ok(result);
    }
}
