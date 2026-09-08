namespace PokerClub.Api.DTOs;

public record RegisterPlayerRequest(
    int TournamentId,
    string? VkId = null,
    string? FirstName = null,
    string? LastName = null,
    string? AvatarUrl = null
);

public record CancelRegistrationRequest(
    int TournamentId,
    string? VkId = null
);

public record AssignPointsRequest(
    int TournamentId,
    Dictionary<int, int> UserPoints
);

public record CreateTournamentRequest(
    string Title,
    int? ClubId = null,
    int? CityId = null,
    string? Address = null,
    string? Format = null,
    decimal BuyIn = 0,
    int MaxSeats = 30,
    DateTime StartTime = default,
    string? Description = null,
    int? StartingChips = null,
    int? BlindLevelMinutes = null
);
