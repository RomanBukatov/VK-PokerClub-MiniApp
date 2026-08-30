namespace PokerClub.Api.DTOs;

public record RegisterPlayerRequest(
    int TournamentId,
    string? VkId = null
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
    int ClubId,
    string Title,
    string? Format,
    decimal BuyIn,
    int MaxSeats,
    DateTime StartTime,
    string? Description = null,
    int? StartingChips = null,
    int? BlindLevelMinutes = null
);
