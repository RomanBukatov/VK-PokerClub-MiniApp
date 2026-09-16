using Microsoft.AspNetCore.Mvc;
using PokerClub.Api.Filters;
using PokerClub.Domain.Interfaces;

namespace PokerClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IGoogleSheetsSyncService _syncService;

    public AdminController(IGoogleSheetsSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost("sync-sheets")]
    [VkAuthorize(RequireAdmin = true)]
    public async Task<ActionResult<GoogleSheetsSyncResult>> SyncGoogleSheets(CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncFromGoogleSheetsAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
