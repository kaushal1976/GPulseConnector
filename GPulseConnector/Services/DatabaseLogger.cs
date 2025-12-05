using Microsoft.EntityFrameworkCore;
using GPulseConnector.Data;
using GPulseConnector.Abstraction.Models;
using Microsoft.Data.SqlClient;

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

        public async Task LogAsync(string message, string level = "Error")
        {
            try
            {
                await using var db = _factory.CreateDbContext();
                db.LogEntries.Add(new LogEntry { Message = message, Level = level });
                await db.SaveChangesAsync();
            }
            catch (SqlException)
            {
                
            }
            catch (ObjectDisposedException)
            {
                
            }
            catch (Exception ex)
            {
                
                _logger.LogError(ex, "Failed to write log to database (non-retry failure)");
                //throw; // optional (remove Throw if you want to continue silently)
            }
        }
    }
}
