namespace PokerClub.Domain.Interfaces;
 
public record GoogleSheetsSyncResult(
    bool Success,
    int TotalProcessed,
    int UpdatedCount,
    int CreatedCount,
    string Message)
{
    public GoogleSheetsSyncResult(int totalProcessed, int updatedCount, int createdCount, string message, bool success = true)
        : this(success, totalProcessed, updatedCount, createdCount, message)
    {
    }

}

public record SyncResultDto(
    int TotalProcessed,
    int UpdatedCount,
    int CreatedCount,
    string Message,
    bool Success = true)
{
    public static implicit operator GoogleSheetsSyncResult(SyncResultDto dto)
        => new GoogleSheetsSyncResult(dto.Success, dto.TotalProcessed, dto.UpdatedCount, dto.CreatedCount, dto.Message);
}

public interface IGoogleSheetsSyncService
{
    Task<GoogleSheetsSyncResult> SyncFromGoogleSheetsAsync(CancellationToken cancellationToken = default);
    Task<GoogleSheetsSyncResult> SyncAsync(CancellationToken cancellationToken = default) => SyncFromGoogleSheetsAsync(cancellationToken);
    Task<GoogleSheetsSyncResult> SyncFromCsvAsync(string csvContent, CancellationToken cancellationToken = default);
    Task<GoogleSheetsSyncResult> SyncFromCsvAsync(string ratingCsvContent, string? registrationsCsvContent, CancellationToken cancellationToken = default);
    Task<GoogleSheetsSyncResult> SyncFromCsvAsync(string? seasonRatingCsvContent, string? totalRatingCsvContent, string? registrationsCsvContent, CancellationToken cancellationToken = default);
}
