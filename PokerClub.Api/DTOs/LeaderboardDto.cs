namespace PokerClub.Api.DTOs;

public record LeaderboardUserDto(
    int Rank,
    int Id,
    string VkId,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    int TotalRating
);
