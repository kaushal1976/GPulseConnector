using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Workers
{
    public class OfflineSyncWorker : BackgroundService
    {
        private readonly IDbContextFactory<OfflineDbContext> _offlineFactory;
        private readonly IDbContextFactory<MainDbContext> _mainFactory;
        private readonly ILogger<OfflineSyncWorker> _logger;

        public OfflineSyncWorker(
        IDbContextFactory<OfflineDbContext> offlineFactory,
        IDbContextFactory<MainDbContext> mainFactory,
        ILogger<OfflineSyncWorker> logger)
        {
            _offlineFactory = offlineFactory;
            _mainFactory = mainFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var offline = _offlineFactory.CreateDbContext();
                    using var main = _mainFactory.CreateDbContext();

                    var pending = await offline.OfflineRecords
                        .OrderBy(r => r.TimeStamp)
                        .ToListAsync(stoppingToken);

                    foreach (var r in pending)
                    {
                        // Try to write to MSSQL
                        main.Records.Add(r);
                        await main.SaveChangesAsync(stoppingToken);

                        // Delete from SQLite after successful write
                        offline.OfflineRecords.Remove(r);
                        await offline.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sync failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
