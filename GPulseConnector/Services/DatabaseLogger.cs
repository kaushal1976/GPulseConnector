using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;
using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Services
{
    public class DatabaseLogger
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<DatabaseLogger> _logger;

        public DatabaseLogger(IDbContextFactory<AppDbContext> factory, ILogger<DatabaseLogger> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task LogAsync(string message, string level = "Information")
        {
            try
            {
                await using var db = _factory.CreateDbContext();
                db.LogEntries.Add(new LogEntry { Message = message, Level = level });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write log to database");
            }
        }
    }
}
