using GPulseConnector.Abstraction.Devices.Brainboxes;
using GPulseConnector.Abstraction.Devices.Mock;
using GPulseConnector.Abstraction.Factories;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Data;
using GPulseConnector.Infrastructure.Devices.Mock;
using GPulseConnector.Options;
using GPulseConnector.Services;
using GPulseConnector.Workers;
using Brainboxes.IO; 
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using GPulseConnector.Extensions;
using GPulseConnector.Services.Sync.Extensions;
using Serilog;
using Microsoft.AspNetCore.DataProtection;
using GPulseConnector;
using System.IO;
using System;;
   
var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------
//APP SETTINGS

builder.Configuration.Sources.Clear();

// Load external configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ---------------------------------------
// OPTIONS
// ---------------------------------------
builder.Services.Configure<DeviceOptions>(
    builder.Configuration.GetSection("Device"));
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection("Database"));

// ---------------------------------------
// CHANNELS
// ---------------------------------------
var inputChannel = Channel.CreateUnbounded<IReadOnlyList<bool>>();
builder.Services.AddSingleton(inputChannel);

var recordChannel = Channel.CreateUnbounded<DeviceRecord>();
builder.Services.AddSingleton(recordChannel);

// ---------------------------------------
// DEVICES
// ---------------------------------------
// Output device (Brainboxes)
builder.Services.AddSingleton<IOutputDevice, BrainboxOutputDevice>();

// Input device (Brainboxes)
builder.Services.AddSingleton<IInputDevice, BrainboxInputDevice>();

// Mock devices for testing
//builder.Services.AddSingleton<IOutputDevice, MockOutputDevice>();
//builder.Services.AddSingleton<IInputDevice, MockInputDevice>();

// ---------------------------------------
// FACTORIES & CACHES
// ---------------------------------------
builder.Services.AddSingleton<DeviceRecordFactory>();
builder.Services.AddSingleton<MachineEventFactory>();
builder.Services.AddSingleton<IPatternMappingCache, PatternMappingCache>();

// ---------------------------------------
// DATABASE CONTEXTS
// ---------------------------------------

// Setup DataProtection
string keysFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "GPulseConnector",
    "keys");

// SQL Server
builder.Services.AddDbContextFactory<DeviceRecordDbContext>(options =>
{
    var dbOpts = builder.Configuration.GetSection("Database")
        .Get<DatabaseOptions>()!;
    options.UseSqlServer(dbOpts.MssqlConnectionString,
        sql => sql.EnableRetryOnFailure());
});

builder.Services.AddDbContext<MainDbContext>(options =>
{
    var dbOpts = builder.Configuration.GetSection("Database")
        .Get<DatabaseOptions>()!;
    options.UseSqlServer(dbOpts.MssqlConnectionString,
        sql => sql.EnableRetryOnFailure());
});

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    //string decryptedConn = appProtector.ReadField("Database:MssqlConnectionString");
        var dbOpts = builder.Configuration.GetSection("Database")
        .Get<DatabaseOptions>()!;
    options.UseSqlServer(dbOpts.MssqlConnectionString,
        sql => sql.EnableRetryOnFailure());
});

// SQLite fallback (optional)
builder.Services.AddDbContextFactory<SQLiteFallbackDbContext>(options =>
{
    var dbOpts = builder.Configuration.GetSection("Database")
        .Get<DatabaseOptions>()!;
    if (dbOpts.EnableSQLiteFallback)
        options.UseSqlite($"Data Source={dbOpts.SqlitePath}");
});

builder.Services.AddDbContext<OfflineDbContext>(options =>
{
    var dbOpts = builder.Configuration.GetSection("Database")
        .Get<DatabaseOptions>()!;
    if (dbOpts.EnableSQLiteFallback)
        options.UseSqlite($"Data Source={dbOpts.SqlitePath}");
});

builder.Services.AddDbContextFactory<RetryQueueDbContext>(options =>
{
    var dbOpts = builder.Configuration.GetSection("Database")
        .Get<DatabaseOptions>()!;
    if (dbOpts.EnableSQLiteFallback)
        options.UseSqlite($"Data Source={dbOpts.SqlitePath}");
});

// ---------------------------------------
// WRITERS
// ---------------------------------------
builder.Services.AddSingleton<SqlMssqlRecordWriter>();
builder.Services.AddSingleton<SqliteFallbackWriter>();
builder.Services.AddSingleton<MachineEventDatabaseWriter>();

builder.Services.AddSingleton<IRecordWriter>(sp =>
    new FailOverRecordWriter(
        sp.GetRequiredService<SqlMssqlRecordWriter>(),
        sp.GetRequiredService<SqliteFallbackWriter>()
    ));

builder.Services.AddSingleton<BufferedRecordWriter>(sp =>
    new BufferedRecordWriter(recordChannel));

builder.Services.AddSingleton<RetryQueueRepository>();
builder.Services.AddSingleton<ReliableDatabaseWriter>();
builder.Services.AddSingleton<DatabaseLogger>();
builder.Services.AddSingleton<Blinker>();


// ---------------------------------------
// WORKERS
// ---------------------------------------
builder.Services.AddHostedService<InputMonitoringWorker>();
builder.Services.AddHostedService<RecordWriterWorker>();
builder.Services.AddHostedService<RetryQueueWorker>();

builder.Services.AddSingleton<OutputUpdateWorker>();
builder.Services.AddSingleton<IHostedService>(sp =>
    sp.GetRequiredService<OutputUpdateWorker>());

builder.Services.AddTableSync();

// ---------------------------------------
// SERILOG
// ---------------------------------------

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/worker-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Logging.AddSerilog();
var app = builder.Build();

// Ensure databases are created at startup
await app.InitialiseDatabasesAsync();
// Run the host
await app.RunAsync();
