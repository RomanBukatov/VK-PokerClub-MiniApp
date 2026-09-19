using Microsoft.Extensions.Primitives;

namespace PokerClub.Infrastructure.Services;

public interface ILeaderboardCacheResetToken
{
    IChangeToken GetExpirationToken();
    void Reset();
}

public class LeaderboardCacheResetToken : ILeaderboardCacheResetToken
{
    private CancellationTokenSource _cts = new();

    public IChangeToken GetExpirationToken() =>
        new CancellationChangeToken(_cts.Token);

    public void Reset()
    {
        var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        try
        {
            oldCts.Cancel();
            oldCts.Dispose();
        }
        catch
        {
            // Игнорируем исключения при отмене токена
        }
    }
}
