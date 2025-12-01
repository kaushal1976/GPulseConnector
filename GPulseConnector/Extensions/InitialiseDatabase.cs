using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GPulseConnector.Options;
using GPulseConnector.Data;
namespace GPulseConnector.Extensions;
using System.Data.SqlClient;
using Microsoft.Data.SqlClient;

public static class InitialiseDatabase
{
    public static async Task InitialiseDatabasesAsync(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("Startup");

        var dbOptions = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    
        if (dbOptions.EnableMSSQL)
        {
            var mssqlFactory = services.GetService<IDbContextFactory<AppDbContext>>();
            if (mssqlFactory != null)
            {
                try
                {
                    await using var mssqlDb = await mssqlFactory.CreateDbContextAsync();

                    var connStringBuilder = new SqlConnectionStringBuilder(mssqlDb.Database.GetConnectionString());
                    var databaseName = connStringBuilder.InitialCatalog;
                    var masterConnectionString = new SqlConnectionStringBuilder(mssqlDb.Database.GetConnectionString())
                    {
                        InitialCatalog = "master"
                    }.ConnectionString;

                    // Connect to master to create DB if missing
                    try
                    {
                        await using var conn = new SqlConnection(masterConnectionString);
                        await conn.OpenAsync();

                        logger?.LogInformation("Connected to MSSQL successfully.");

                        var cmd = conn.CreateCommand();
                        cmd.CommandText = $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}]";
                        await cmd.ExecuteNonQueryAsync();

                        logger?.LogInformation("MSSQL database ensured/created successfully.");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to create MSSQL database. Skipping.");
                        goto SkipMssqlEnsureCreated;
                    }

                    // Now run EnsureCreatedAsync to create tables
                    try
                    {
                        await mssqlDb.Database.EnsureCreatedAsync();
                        logger?.LogInformation("MSSQL tables ensured successfully.");
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to ensure MSSQL tables.");
                    }

                SkipMssqlEnsureCreated:;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "MSSQL unreachable or DbContext creation failed. Skipping database initialization.");
                }
            }
            else
            {
                logger?.LogWarning("MSSQL enabled but AppDbContext is not registered.");
            }
        }

        if (dbOptions.EnableSQLiteFallback)
        {
            var sqliteFactory = services.GetService<IDbContextFactory<SQLiteFallbackDbContext>>();
            if (sqliteFactory != null)
            {
                try
                {
                    await using var sqliteDb = await sqliteFactory.CreateDbContextAsync();
                    await sqliteDb.Database.EnsureCreatedAsync();
                    logger?.LogInformation("SQLite fallback database ensured/created successfully.");
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "SQLite fallback database creation failed.");
                }
            }
            else
            {
                logger?.LogWarning("SQLite fallback enabled but SQLiteFallbackDbContext is not registered.");
            }
        }
        
    }
}
