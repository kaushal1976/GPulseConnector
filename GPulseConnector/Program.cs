using Brainboxes.IO;
using GPulseConnector.Abstraction.Devices.Brainboxes;
using GPulseConnector.Abstraction.Devices.Mock;
using GPulseConnector.Abstraction.Factories;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Data;
using GPulseConnector.Extensions;
using GPulseConnector.Infrastructure.Devices.Mock;
using GPulseConnector.Options;
using GPulseConnector.Services;
using GPulseConnector.Services.Sync.Extensions;
using GPulseConnector.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Threading.Channels;
using Serilog.Events;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService() 
    .ConfigureAppConfiguration((context, config) =>
    {
        config.Sources.Clear();

        config
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // -----------------------------
        // OPTIONS
        // -----------------------------
        services.Configure<DeviceOptions>(configuration.GetSection("Device"));
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));

        // -----------------------------
        // CHANNELS
        // -----------------------------
        var inputChannel = Channel.CreateUnbounded<IReadOnlyList<bool>>();
        services.AddSingleton(inputChannel);


        // -----------------------------
        // DEVICES
        // -----------------------------
        services.AddSingleton<IOutputDevice, BrainboxOutputDevice>();
        services.AddSingleton<IInputDevice, BrainboxInputDevice>();

        // -----------------------------
        // FACTORIES & CACHES
        // -----------------------------

        services.AddSingleton<MachineEventFactory>();
        services.AddSingleton<IPatternMappingCache, PatternMappingCache>();

        // -----------------------------
        // DATABASE CONTEXTS
        // -----------------------------

        var dbOpts = configuration.GetSection("Database").Get<DatabaseOptions>()!;

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(AesEncryption.Decrypt(dbOpts.MssqlConnectionString).Trim(), sql => sql.EnableRetryOnFailure())
            .LogTo(
                message => {
                // Only log messages that are NOT Executed DbCommand
                    if (!message.StartsWith("Executed DbCommand"))
                    {
                        Serilog.Log.Information(message);
                    }
                },  
                LogLevel.Error                  
                )
            );

        if (dbOpts.EnableSQLiteFallback)
        {
            services.AddDbContextFactory<SQLiteFallbackDbContext>(options =>
                options.UseSqlite($"Data Source={dbOpts.SqlitePath}")
                .LogTo(
                    message => {
                    // Only log messages that are NOT Executed DbCommand
                        if (!message.StartsWith("Executed DbCommand"))
                        {
                            Serilog.Log.Information(message);
                        }
                    },  
                    LogLevel.Error
                )
                );

            services.AddDbContextFactory<RetryQueueDbContext>(options =>
                options.UseSqlite($"Data Source={dbOpts.SqlitePath}")
                .LogTo(
                message => {
                // Only log messages that are NOT Executed DbCommand
                    if (!message.StartsWith("Executed DbCommand"))
                    {
                        Serilog.Log.Information(message);
                    }
                },  
                LogLevel.Error                  
                )
                );
        }

        // -----------------------------
        // WRITERS
        // -----------------------------
        

        services.AddSingleton<RetryQueueRepository>();
        services.AddSingleton<MachineEventDatabaseWriter>();
        services.AddSingleton<ReliableDatabaseWriter>();
        services.AddSingleton<DatabaseLogger>();
        services.AddSingleton<Blinker>();

        // -----------------------------
        // WORKERS
        // -----------------------------
        services.AddHostedService<InputMonitoringWorker>();
        services.AddHostedService<RecordWriterWorker>();
        services.AddHostedService<RetryQueueWorker>();

        services.AddSingleton<OutputUpdateWorker>();
        services.AddSingleton<IHostedService>(sp =>
            sp.GetRequiredService<OutputUpdateWorker>());

        services.AddTableSync();
    })
    .UseSerilog((context, services, loggerConfig) =>
    {
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);

        loggerConfig
        .MinimumLevel.Information() // general minimum level
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)// EF Core command logs
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Infrastructure", LogEventLevel.Error) // EF Core infrastructure logs
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning) // EF Core general logs
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(logDir, "log-.log"), rollingInterval: RollingInterval.Day);

    })
    .Build();

// -----------------------------
// INITIALIZE DATABASES
// -----------------------------
await host.InitialiseDatabasesAsync();

// -----------------------------
// RUN
// -----------------------------
await host.RunAsync();
