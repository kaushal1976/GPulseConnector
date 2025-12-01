using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;
using GPulseConnector.Services.Sync;

namespace GPulseConnector.Workers
{
    public class MultiTableSyncWorker : BackgroundService
    {
        private readonly IEnumerable<ISyncTableConfig> _tables;
        private readonly IDbContextFactory<AppDbContext> _msFactory;
        private readonly IDbContextFactory<SQLiteFallbackDbContext> _sqliteFactory;
        private readonly ILogger<MultiTableSyncWorker> _log;

        public MultiTableSyncWorker(
            IEnumerable<ISyncTableConfig> tables,
            IDbContextFactory<AppDbContext> msFactory,
            IDbContextFactory<SQLiteFallbackDbContext> sqliteFactory,
            ILogger<MultiTableSyncWorker> log)
        {
            _tables = tables;
            _msFactory = msFactory;
            _sqliteFactory = sqliteFactory;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("Starting multi-table sync worker.");
            await RunSync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // next run at 2AM
                var now = DateTime.Now;
                var next = now.Date.AddHours(2);
                if (next <= now) next = next.AddDays(1);

                await Task.Delay(next - now, stoppingToken);
                await RunSync(stoppingToken);
            }
        }

        private async Task RunSync(CancellationToken ct)
        {
            foreach (var t in _tables)
            {
                try
                {
                    await t.SyncAsync(_msFactory, _sqliteFactory, ct);
                    _log.LogInformation("Synced table via {t}", t.GetType().Name);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to sync table: {t}", t.GetType().Name);
                }
            }
        }
    }
}
