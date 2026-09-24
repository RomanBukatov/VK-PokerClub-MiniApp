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
    bool IsUserRegistered = false,
    DateTime? RegistrationEnd = null,
    string? ClubAddress = null,
    int StartingStack = 10000
)
{
    public int StartingChips => StartingStack > 0 ? StartingStack : 10000;
}

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
    List<RegisteredPlayerDto> Participants,
    DateTime? RegistrationEnd = null,
    int StartingStack = 10000
)
{
    public int StartingChips => StartingStack > 0 ? StartingStack : 10000;
}

public record RegisteredPlayerDto(
    int UserId,
    string VkId,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    int TotalRating,
    DateTime RegisteredAt,
    int PointsEarned = 0
);
