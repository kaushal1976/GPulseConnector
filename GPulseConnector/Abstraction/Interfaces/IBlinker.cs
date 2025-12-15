public interface IBlinker : IDisposable
{
    Task StartOrRestartAsync(
        IReadOnlyList<bool> values,
        Func<IReadOnlyList<bool>, Task> callback,
        int blinkIntervalMs,
        int blinkDurationMs,
        CancellationToken stoppingToken);
}