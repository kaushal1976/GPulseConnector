using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;

namespace GPulseConnector.Services.Sync
{
    public interface ISyncTableConfig
    {
        Task SyncAsync(
            IDbContextFactory<AppDbContext> msFactory,
            IDbContextFactory<SQLiteFallbackDbContext> sqliteFactory,
            CancellationToken ct);
    }
}
