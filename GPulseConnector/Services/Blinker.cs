using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GPulseConnector.Abstraction.Interfaces;

namespace GPulseConnector.Services;

public sealed class Blinker : IDisposable
{
    private readonly object _lock = new object();
    private CancellationTokenSource? _activeCts;
    private bool _disposed;

    /// <summary>
    /// Starts blinking the final values. Immediately cancels any previous blink.
    /// Does NOT wait for previous blink to complete.
    /// </summary>
    public Task StartOrRestartAsync(
        IReadOnlyList<bool> finalValues,
        Func<IReadOnlyList<bool>, Task> tickCallback,
        int blinkIntervalMs,
        int blinkDurationMs,
        CancellationToken externalCancel = default)
        
    {
        if (finalValues == null || finalValues.Count == 0)
            throw new ArgumentException("finalValues cannot be null or empty.", nameof(finalValues));
        
        if (tickCallback == null)
            throw new ArgumentNullException(nameof(tickCallback));

        CancellationTokenSource newCts;

        lock (_lock)
        {
            // Cancel previous blink immediately
            _activeCts?.Cancel();
            _activeCts?.Dispose();

            // Create new cancellation token
            newCts = externalCancel.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(externalCancel)
                : new CancellationTokenSource();

            _activeCts = newCts;
        }

        // Start new blink without awaiting (fire-and-forget but tracked)
        _ = ExecuteBlinkAsync(finalValues, tickCallback, blinkIntervalMs, blinkDurationMs, newCts.Token);

        return Task.CompletedTask;
    }

    /// Stops any ongoing blink operation.
    public void Stop()
    {
        lock (_lock)
        {
            _activeCts?.Cancel();
        }
    }

    public bool IsBlinking
    {
        get
        {
            lock (_lock)
            {
                return _activeCts != null && !_activeCts.Token.IsCancellationRequested;
            }
        }
    }

    private async Task ExecuteBlinkAsync(
        IReadOnlyList<bool> finalValues,
        Func<IReadOnlyList<bool>, Task> tickCallback,
        int blinkIntervalMs,
        int blinkDurationMs,
        CancellationToken token)
    {
        var blankValues = new bool[finalValues.Count];
        var stopwatch = Stopwatch.StartNew();
        bool showValues = true;
        bool completedNaturally = false;

        try
        {
            // Initial display
            if (!token.IsCancellationRequested)
            {
                await InvokeTick(tickCallback, finalValues);
            }

            // Blink loop
            while (stopwatch.ElapsedMilliseconds < blinkDurationMs)
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    await Task.Delay(blinkIntervalMs, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested)
                    return;

                showValues = !showValues;
                var valuesToShow = showValues ? finalValues : blankValues;
                await InvokeTick(tickCallback, valuesToShow);
            }

            completedNaturally = true;
        }
        catch (TaskCanceledException)
        {
            // Expected, exit silently
        }
        catch (OperationCanceledException)
        {
            // Expected, exit silently
        }

        // Show final values only if completed naturally
        if (completedNaturally && !token.IsCancellationRequested)
        {
            try
            {
                await InvokeTick(tickCallback, finalValues);
            }
            catch
            {
                // Ignore final tick errors
            }
        }
    }

    private static async Task InvokeTick(
        Func<IReadOnlyList<bool>, Task> tickCallback,
        IReadOnlyList<bool> values)
    {
        try
        {
            await tickCallback(values);
        }
        catch
        {
            // Swallow callback errors to prevent crash
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_lock)
        {
            _activeCts?.Cancel();
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }
}