using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using GPulseConnector.Data;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Services;

namespace GPulseConnector.Workers
{
    public class RetryQueueWorker : BackgroundService
    {
        private readonly ReliableDatabaseWriter _writer;
        private readonly RetryQueueRepository _retryRepo;
        private readonly int _retryIntervalSeconds;
        private readonly ILogger<RetryQueueWorker> _logger;
        private readonly DatabaseLogger _dblogger;
        private readonly int _maxRetryAttempts;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public RetryQueueWorker(ReliableDatabaseWriter writer, RetryQueueRepository retryRepo, IConfiguration config, ILogger<RetryQueueWorker> logger, DatabaseLogger dblogger, IDbContextFactory<AppDbContext> factory)
        {
            _writer = writer;
            _retryRepo = retryRepo;
            _logger = logger;
            _dblogger = dblogger;
            _factory = factory;

            _retryIntervalSeconds = config.GetValue("RetrySettings:RetryIntervalSeconds", 30);
            _maxRetryAttempts = config.GetValue("RetrySettings:MaxRetryAttempts", 5000);
            _factory = factory;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Retry worker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var items = await _retryRepo.LoadAllAsync();  // returns retry metadata + payload bytes

                    foreach (var item in items)
                    {
                        try
                        {
                            // Deserialize to object
                            var payload = RetryQueueRepository.Deserialize(item);

                            if (payload is not null)
                            {
                                // Dynamic dispatch to generic writer
                                var type = payload.GetType();

                                // Using reflection to call WriteSafeAsync<T>(T item)
                                var method = _writer.GetType()
                                    .GetMethod(nameof(_writer.WriteSafeAsync))
                                    !.MakeGenericMethod(type);

                                var task = (Task<bool>)method.Invoke(_writer, new[] { payload })!;
                                bool ok = await task;

                                if (ok)
                                {
                                    await _retryRepo.DeleteAsync(item);
                                    await _dblogger.LogAsync(
                                        $"Retried {type.Name} successfully (Item {item.Id}).",
                                        "Information");
                                }
                                else
                                {
                                    await _retryRepo.RecordFailureAsync(item, "Write failed and was re-queued");
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Failed to deserialize retry item {Id}", item.Id);
                                await _retryRepo.RecordFailureAsync(item, "Deserialization returned null");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Retry attempt failed for item {Id}", item.Id);
                            await _retryRepo.RecordFailureAsync(item, ex.Message);
                        }

                        // Dead-letter logic
                        if (item.AttemptCount >= _maxRetryAttempts)
                        {
                            _logger.LogWarning(
                                "Dropping retry item {Id} after {Attempts} attempts",
                                item.Id, item.AttemptCount);

                            await _dblogger.LogAsync(
                                $"Dropping retry item {item.Id} after {item.AttemptCount} attempts.",
                                "Warning");

                            await _retryRepo.DeleteAsync(item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in retry loop");
                }

                await Task.Delay(TimeSpan.FromSeconds(_retryIntervalSeconds), stoppingToken);
            }
        }

    }
}
