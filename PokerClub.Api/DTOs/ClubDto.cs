namespace PokerClub.Api.DTOs;

public record ClubDto(
    int Id,
    int CityId,
    string Name,
    string? Address,
    bool IsActive,
    string? CityName
);
