namespace PokerClub.Api.DTOs;

public record CityDto(
    int Id,
    string Name,
    string Slug,
    bool IsActive,
    int ActiveClubsCount
);
