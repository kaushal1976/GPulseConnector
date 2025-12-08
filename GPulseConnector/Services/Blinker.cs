using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GPulseConnector.Services
{
    public sealed class Blinker : IDisposable
    {
        private readonly Channel<BlinkRequest> _channel =
            Channel.CreateBounded<BlinkRequest>(
                new BoundedChannelOptions(1)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                });

        private readonly object _sync = new();
        private CancellationTokenSource? _workerCts;
        private CancellationTokenSource? _activeRequestCts;
        private Task? _workerTask;
        private bool _disposed;

        public bool IsRunning
        {
            get
            {
                lock (_sync)
                    return _workerTask is { IsCompleted: false };
            }
        }

        // ===================================================================
        // PUBLIC API
        // ===================================================================
        public Task StartOrRestartAsync(
            IReadOnlyList<bool> input,
            IReadOnlyList<bool> finalValues,
            Func<IReadOnlyList<bool>, Task> tickCallback,
            int blinkIntervalMs,
            int blinkDurationMs,
            CancellationToken externalCancel = default)
        {
            if (input.Count != finalValues.Count)
                throw new ArgumentException("input and finalValues must match in length.");

            EnsureWorkerStarted();

            // Cancel the previous request's token
            CancellationTokenSource requestCts;
            lock (_sync)
            {
                _activeRequestCts?.Cancel();
                _activeRequestCts?.Dispose();

                _activeRequestCts = externalCancel.CanBeCanceled
                    ? CancellationTokenSource.CreateLinkedTokenSource(externalCancel)
                    : new CancellationTokenSource();

                requestCts = _activeRequestCts;
            }

            var req = new BlinkRequest(
                input,
                finalValues,
                tickCallback,
                requestCts,
                blinkIntervalMs,
                blinkDurationMs
            );

            if (!_channel.Writer.TryWrite(req))
                _ = _channel.Writer.WriteAsync(req);

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            Task? workerCopy = null;

            lock (_sync)
            {
                if (_workerTask == null || _workerTask.IsCompleted)
                    return;

                _activeRequestCts?.Cancel();
                _workerCts?.Cancel();

                workerCopy = _workerTask;
            }

            try { await workerCopy; } catch { }
        }

        // ===================================================================
        // WORKER SETUP
        // ===================================================================
        private void EnsureWorkerStarted()
        {
            lock (_sync)
            {
                if (_workerTask != null && !_workerTask.IsCompleted)
                    return;

                _workerCts = new CancellationTokenSource();
                _workerTask = Task.Run(() => WorkerLoopAsync(_workerCts.Token));
            }
        }

        private async Task WorkerLoopAsync(CancellationToken workerToken)
        {
            var reader = _channel.Reader;

            try
            {
                while (await reader.WaitToReadAsync(workerToken))
                {
                    if (!reader.TryRead(out var req))
                        continue;

                    // Skip cancelled requests
                    if (req.CancelToken.IsCancellationRequested)
                        continue;

                    try
                    {
                        await ProcessRequestAsync(req, workerToken);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Blinker Error: {ex}");
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        // ===================================================================
        // BLINK PROCESSOR (now with duration & speed)
        // ===================================================================
        private async Task ProcessRequestAsync(BlinkRequest req, CancellationToken workerToken)
        {
            bool completedNaturally = false;

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(workerToken, req.CancelToken.Token);
            var token = linked.Token;

            var blinkIndexes = req.Input
                .Select((v, i) => v ? i : -1)
                .Where(i => i >= 0)
                .ToArray();

            bool[] buffer = new bool[req.Input.Count];

            if (blinkIndexes.Length == 0)
            {
                await SafeTick(req.TickCallback, req.FinalValues, token);
                return;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                while (!token.IsCancellationRequested &&
                       stopwatch.ElapsedMilliseconds < req.BlinkDurationMs)
                {
                    foreach (var idx in blinkIndexes)
                        buffer[idx] = !buffer[idx];

                    await SafeTick(req.TickCallback, buffer, token);

                    await Task.Delay(req.BlinkIntervalMs, token);
                }

                // If loop exited by time, not cancellation:
                if (!token.IsCancellationRequested)
                    completedNaturally = true;
            }
            catch (OperationCanceledException) { }


            // Final apply ONLY if blinking finished by duration, not cancelled.
            if (!linked.IsCancellationRequested &&
                stopwatch.ElapsedMilliseconds >= req.BlinkDurationMs)
            {
                await SafeTick(req.TickCallback, req.FinalValues.ToArray(), CancellationToken.None);
            }
        }


        private static async Task SafeTick(
            Func<IReadOnlyList<bool>, Task> cb,
            IReadOnlyList<bool> values,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await cb(values);
        }

        // ===================================================================
        // DISPOSAL
        // ===================================================================
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_sync)
            {
                _activeRequestCts?.Cancel();
                _activeRequestCts?.Dispose();
                _workerCts?.Cancel();
            }

            try { _workerTask?.Wait(200); } catch { }
        }

        // ===================================================================
        // INTERNAL STRUCT
        // ===================================================================
        private sealed record BlinkRequest(
            IReadOnlyList<bool> Input,
            IReadOnlyList<bool> FinalValues,
            Func<IReadOnlyList<bool>, Task> TickCallback,
            CancellationTokenSource CancelToken,
            int BlinkIntervalMs,
            int BlinkDurationMs);
    }
}
