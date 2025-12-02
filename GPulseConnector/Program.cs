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

        var recordChannel = Channel.CreateUnbounded<DeviceRecord>();
        services.AddSingleton(recordChannel);

        // -----------------------------
        // DEVICES
        // -----------------------------
        services.AddSingleton<IOutputDevice, BrainboxOutputDevice>();
        services.AddSingleton<IInputDevice, BrainboxInputDevice>();

        // -----------------------------
        // FACTORIES & CACHES
        // -----------------------------
        services.AddSingleton<DeviceRecordFactory>();
        services.AddSingleton<MachineEventFactory>();
        services.AddSingleton<IPatternMappingCache, PatternMappingCache>();

        // -----------------------------
        // DATABASE CONTEXTS
        // -----------------------------
        var dbOpts = configuration.GetSection("Database").Get<DatabaseOptions>()!;

        services.AddDbContextFactory<DeviceRecordDbContext>(options =>
            options.UseSqlServer(dbOpts.MssqlConnectionString, sql => sql.EnableRetryOnFailure()));

        services.AddDbContext<MainDbContext>(options =>
            options.UseSqlServer(dbOpts.MssqlConnectionString, sql => sql.EnableRetryOnFailure()));

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(dbOpts.MssqlConnectionString, sql => sql.EnableRetryOnFailure()));

        if (dbOpts.EnableSQLiteFallback)
        {
            services.AddDbContextFactory<SQLiteFallbackDbContext>(options =>
                options.UseSqlite($"Data Source={dbOpts.SqlitePath}"));

            services.AddDbContext<OfflineDbContext>(options =>
                options.UseSqlite($"Data Source={dbOpts.SqlitePath}"));

            services.AddDbContextFactory<RetryQueueDbContext>(options =>
                options.UseSqlite($"Data Source={dbOpts.SqlitePath}"));
        }

        // -----------------------------
        // WRITERS
        // -----------------------------
        services.AddSingleton<SqlMssqlRecordWriter>();
        services.AddSingleton<SqliteFallbackWriter>();
        services.AddSingleton<MachineEventDatabaseWriter>();

        services.AddSingleton<IRecordWriter>(sp =>
            new FailOverRecordWriter(
                sp.GetRequiredService<SqlMssqlRecordWriter>(),
                sp.GetRequiredService<SqliteFallbackWriter>()
            ));

        services.AddSingleton<BufferedRecordWriter>(sp =>
            new BufferedRecordWriter(recordChannel));

        services.AddSingleton<RetryQueueRepository>();
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

        loggerConfig.WriteTo.File(Path.Combine(logDir, "log-.log"), rollingInterval: RollingInterval.Day);
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
