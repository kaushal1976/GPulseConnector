using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPulseConnector.Abstraction.Interfaces;
using Microsoft.Extensions.Logging;

namespace GPulseConnector.Abstraction.Devices.Mock
{
    public class MockInputDevice : IInputDevice, IDisposable
    {
        private readonly ILogger<MockInputDevice> _logger;
        private readonly Random _rand = new();
        private readonly int _numInputs;

        private CancellationTokenSource? _monitorCts;
        private Task? _monitorTask;

        private readonly object _connLock = new();
        private bool _isConnected = false;

        // Chance of disconnect on each update
        public double RandomDisconnectChance { get; set; } = 0.02;

        // Interval of normal updates
        public int MonitoringIntervalMs { get; set; } = 500;

        // Minimum time to stay disconnected (ms)
        public int MinDisconnectDurationMs { get; set; } = 3000;

        // Reconnect attempt interval (ms)
        public int ReconnectAttemptIntervalMs { get; set; } = 1500;

        public event Action<IReadOnlyList<bool>>? InputsChanged;
        public event Action<bool>? DeviceDisconnected;

        public MockInputDevice(ILogger<MockInputDevice> logger, int numInputs = 16)
        {
            _logger = logger;
            _numInputs = numInputs;
        }

        public bool IsConnected
        {
            get { lock (_connLock) return _isConnected; }
            private set { lock (_connLock) _isConnected = value; }
        }

        public Task ConnectAsync(CancellationToken token = default)
        {
            // Real connection happens in EnsureConnectedAsync
            return Task.CompletedTask;
        }

        public Task StartMonitoringAsync(CancellationToken token = default)
        {
            StopMonitoringInternal();
            _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token));
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<bool>> ReadInputsAsync(CancellationToken token = default)
        {
            await EnsureConnectedAsync(token);

            return Enumerable.Range(0, _numInputs)
                .Select(_ => _rand.Next(0, 2) == 1)
                .ToList()
                .AsReadOnly();
        }


        // =============================================================
        // MONITORING LOOP
        // =============================================================

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            _logger.LogInformation("MockInputDevice: Monitoring started");

            await EnsureConnectedAsync(token);

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(MonitoringIntervalMs, token);

                // Random disconnect
                if (IsConnected && _rand.NextDouble() < RandomDisconnectChance)
                {
                    _logger.LogWarning("MockInputDevice: Random disconnect triggered");
                    await HandleDisconnectAsync(token);
                    continue;
                }

                if (!IsConnected)
                {
                    // Should not happen outside explicit disconnect, but just in case
                    await AttemptReconnectLoopAsync(token);
                    continue;
                }

                // Normal input update
                var values = Enumerable.Range(0, _numInputs)
                    .Select(_ => _rand.Next(0, 2) == 1)
                    .ToList()
                    .AsReadOnly();

                InputsChanged?.Invoke(values);
            }
        }


        // =============================================================
        // DISCONNECT + RECONNECT DURING DISCONNECTION
        // =============================================================

        private async Task HandleDisconnectAsync(CancellationToken token)
        {
            if (!IsConnected)
                return;

            IsConnected = false;
            DeviceDisconnected?.Invoke(true);
            _logger.LogError("MockInputDevice: DISCONNECTED");

            var disconnectStart = DateTime.UtcNow;

            while (!IsConnected && !token.IsCancellationRequested)
            {
                var elapsed = (DateTime.UtcNow - disconnectStart).TotalMilliseconds;

                // Try reconnecting during the disconnection window
                if (await TryReconnectAsync(token))
                {
                    // If reconnect succeeds early, break immediately
                    _logger.LogInformation("MockInputDevice: Reconnected EARLY");
                    return;
                }

                // Must wait at least MinDisconnectDurationMs before reconnect can succeed
                if (elapsed < MinDisconnectDurationMs)
                {
                    _logger.LogInformation(
                        $"MockInputDevice: Reconnect attempt blocked (min downtime). Elapsed={elapsed:0} ms"
                    );
                }
                else
                {
                    // After minimum downtime, reconnect attempts can succeed
                    _logger.LogInformation("MockInputDevice: Allowed to reconnect now.");
                    await AttemptReconnectLoopAsync(token);
                    return;
                }

                await Task.Delay(ReconnectAttemptIntervalMs, token);
            }
        }


        private async Task<bool> TryReconnectAsync(CancellationToken token)
        {
            await Task.Delay(100, token); // simulate connection attempt time

            // 25% chance reconnect attempt "succeeds" (before min downtime enforced)
            bool success = _rand.NextDouble() < 0.25;

            if (success)
            {
                _logger.LogInformation("MockInputDevice: Reconnect attempt succeeded");
            }
            else
            {
                _logger.LogWarning("MockInputDevice: Reconnect attempt failed");
            }

            return success;
        }


        // =============================================================
        // RECONNECT LOOP (after minimum downtime)
        // =============================================================

        private async Task AttemptReconnectLoopAsync(CancellationToken token)
        {
            while (!IsConnected && !token.IsCancellationRequested)
            {
                _logger.LogInformation("MockInputDevice: Attempting reconnect...");

                await Task.Delay(300, token); // connection attempt

                // HERE: full reconnect always succeeds
                IsConnected = true;
                DeviceDisconnected?.Invoke(false);

                _logger.LogInformation("MockInputDevice: FULL reconnect succeeded.");
                return;
            }
        }


        private async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (!IsConnected)
                await AttemptReconnectLoopAsync(token);
        }


        // =============================================================
        // CLEANUP
        // =============================================================

        private void StopMonitoringInternal()
        {
            try { _monitorCts?.Cancel(); } catch { }
            try { _monitorTask?.Wait(200); } catch { }

            _monitorCts?.Dispose();
            _monitorCts = null;
            _monitorTask = null;
        }

        public void Dispose()
        {
            StopMonitoringInternal();
        }
    }
}
