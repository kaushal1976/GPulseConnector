using GPulseConnector.Services.Sync;
using Microsoft.Extensions.DependencyInjection;
using GPulseConnector.Data;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Workers;

namespace GPulseConnector.Services.Sync.Extensions
{
    public static class SyncRegistrationExtensions
    {
        public static IServiceCollection AddTableSync(this IServiceCollection services)
        {
            // Register all table sync configs in one place
            services.AddSingleton<IEnumerable<ISyncTableConfig>>(sp =>
            {
                return new List<ISyncTableConfig>
                {
                    new SyncTableConfig<PatternMapping>
                    {
                        MsQuery     = db => db.PatternMappings,
                        SqliteQuery = db => db.PatternMappings,
                        SqliteSet   = db => db.PatternMappings,
                        Key         = e => e.Id,
                        ComputeHash = e => e.ComputeHash()
                    },
                
                    // Add more tables here
                };
            });

            // Background worker that performs nightly sync
            services.AddHostedService<MultiTableSyncWorker>();

            return services;
        }
    }
}
