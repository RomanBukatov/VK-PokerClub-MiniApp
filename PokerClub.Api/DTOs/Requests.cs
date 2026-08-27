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
