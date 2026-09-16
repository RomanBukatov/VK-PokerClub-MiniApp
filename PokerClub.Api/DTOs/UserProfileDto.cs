namespace PokerClub.Api.DTOs;

public record UserProfileDto(
    int Id,
    string VkId,
    string? FirstName,
    string? LastName,
    string? FullName,
    string? Nickname,
    string? PhoneNumber,
    string? ClubCardId,
    string? AvatarUrl,
    int TotalRating,
    string Status,
    DateTime? AcceptedTermsAt,
    int TournamentsPlayed,
    int WinsCount,
    int Top3Count,
    int Top10Count,
    int KnockoutsCount,
    double AvgPlace,
    DateTime CreatedAt
);
