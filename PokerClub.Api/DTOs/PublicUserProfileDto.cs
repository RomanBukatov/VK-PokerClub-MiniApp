namespace PokerClub.Api.DTOs;

public record PublicUserProfileDto(
    int Id,
    string? Nickname,
    string? FirstName,
    string? LastName,
    int SeasonRating,
    int TotalRating,
    int TournamentsPlayed,
    int WinsCount,
    int Top3Count,
    int Top10Count,
    int KnockoutsCount,
    double AvgPlace,
    string? AvatarUrl = null,
    string? VkId = null,
    string? ClubCardId = null,
    string? PhoneNumber = null
);
