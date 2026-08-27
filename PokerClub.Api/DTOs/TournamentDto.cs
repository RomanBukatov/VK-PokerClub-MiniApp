using PokerClub.Domain.Enums;

namespace PokerClub.Api.DTOs;

public record TournamentScheduleDto(
    int Id,
    string Title,
    string? Format,
    decimal BuyIn,
    string? Description,
    int MaxSeats,
    DateTime StartTime,
    TournamentStatus Status,
    int ClubId,
    string? ClubName,
    string? CityName,
    int RegisteredCount,
    bool IsUserRegistered = false
);

public record TournamentDetailDto(
    int Id,
    string Title,
    string? Format,
    decimal BuyIn,
    string? Description,
    int MaxSeats,
    DateTime StartTime,
    TournamentStatus Status,
    int ClubId,
    string? ClubName,
    string? CityName,
    string? ClubAddress,
    int RegisteredCount,
    bool IsUserRegistered,
    List<RegisteredPlayerDto> Participants
);

public record RegisteredPlayerDto(
    int UserId,
    string VkId,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    int TotalRating,
    DateTime RegisteredAt
);
