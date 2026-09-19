namespace PokerClub.Api.DTOs;

public record LeaderboardUserDto(
    int Rank,
    int Id,
    string VkId,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    int TotalRating,
    int SeasonRating = 0,
    int Points = 0
);

public record LeaderboardEntryDto(
    int Rank,
    int Id,
    string VkId,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    int TotalRating,
    int SeasonRating = 0,
    int Points = 0
);

public record LeaderboardResponseDto(
    List<LeaderboardEntryDto> Items,
    int TotalCount,
    int Limit,
    int Offset,
    string SeasonName = "Осень 2026"
);
