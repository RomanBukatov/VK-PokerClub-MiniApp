using PokerClub.Domain.Enums;

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
    int? StartingStack = null,
    int? BlindLevelMinutes = null,
    DateTime? RegistrationEnd = null
);

public record UpdateTournamentRequest(
    string? Title = null,
    int? ClubId = null,
    int? CityId = null,
    string? Address = null,
    string? Format = null,
    decimal? BuyIn = null,
    int? MaxSeats = null,
    DateTime? StartTime = null,
    string? Description = null,
    int? StartingChips = null,
    int? StartingStack = null,
    int? BlindLevelMinutes = null,
    DateTime? RegistrationEnd = null,
    TournamentStatus? Status = null,
    bool ClearRegistrationEnd = false
);

public record UpdateProfileRequest(
    string? Nickname = null,
    string? FullName = null,
    string? FirstName = null,
    string? LastName = null,
    string? PhoneNumber = null,
    string? ClubCardId = null,
    string? AvatarUrl = null,
    bool? AcceptedTerms = null,
    DateTime? AcceptedTermsAt = null
);

public record AcceptTermsRequest(
    DateTime? AcceptedAt = null
);

