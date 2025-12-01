using GPulseConnector.Abstraction.Interfaces;
using Microsoft.Extensions.Logging;

namespace GPulseConnector.Infrastructure.Devices.Mock
{
    public class MockOutputDevice : IOutputDevice
    {
        private readonly ILogger<MockOutputDevice>? _logger;
        private readonly bool[] _outputs;
        private readonly Random _rand = new();

        private CancellationTokenSource? _monitorCts;
        private Task? _monitorTask;

        // --- Connection state ---
        public bool IsConnected { get; private set; } = false;

        // --- Timing configuration ---
        private const int MonitorIntervalMs = 500;             // frequency of monitoring
        private const int ReconnectAttemptIntervalMs = 500;    // period between reconnect attempts
        private const int MinDisconnectDurationMs = 3000;      // forced downtime
        private const double RandomDisconnectChance = 0.1;    // 5% chance each cycle

        public int OutputCount => _outputs.Length;

        public event Action<int, bool>? OutputChanged;
        public event Action<bool>? DeviceDisconnected;

        public MockOutputDevice(ILogger<MockOutputDevice>? logger = null, int outputCount = 16)
        {
            _logger = logger;
            _outputs = new bool[outputCount];
        }

        // --------------------------------------------------------------------
        //  PUBLIC DEVICE METHODS
        // --------------------------------------------------------------------

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            _logger?.LogInformation("MockOutputDevice: Connected");

            // Start monitoring loops
            _monitorCts?.Cancel();
            _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token), cancellationToken);

            return Task.CompletedTask;
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            _logger?.LogWarning("MockOutputDevice: Disconnect requested");

            if (_monitorCts != null)
            {
                _monitorCts.Cancel();
                try { await _monitorTask!; }
                catch { /* swallow */ }
            }

            DeviceDisconnected?.Invoke(true);
        }

        public Task<IReadOnlyList<bool>> ReadOutputsAsync(CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            return Task.FromResult((IReadOnlyList<bool>)_outputs);
        }

        public Task SetOutputAsync(int outputIndex, bool value, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            if (outputIndex < 0 || outputIndex >= _outputs.Length)
                throw new ArgumentOutOfRangeException(nameof(outputIndex));

            if (_outputs[outputIndex] != value)
            {
                _outputs[outputIndex] = value;
                OutputChanged?.Invoke(outputIndex, value);
                _logger?.LogDebug($"MockOutputDevice: Output {outputIndex} = {value}");
            }

            return Task.CompletedTask;
        }

        public Task SetOutputsAsync(IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            if (values.Count != _outputs.Length)
                throw new ArgumentException($"Expected {OutputCount} outputs but received {values.Count}.");

            for (int i = 0; i < values.Count; i++)
            {
                if (_outputs[i] != values[i])
                {
                    _outputs[i] = values[i];
                    OutputChanged?.Invoke(i, values[i]);
                }
            }

            return Task.CompletedTask;
        }

        public void SimulateOutputChange(int index, bool value)
        {
            if (index < 0 || index >= _outputs.Length) 
                throw new ArgumentOutOfRangeException(nameof(index));

            _outputs[index] = value;
            OutputChanged?.Invoke(index, value);
        }

        // --------------------------------------------------------------------
        //  INTERNAL UTILITY
        // --------------------------------------------------------------------

        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("MockOutputDevice is not connected.");
        }

        // --------------------------------------------------------------------
        //  MONITOR LOOP – detects random disconnections
        // --------------------------------------------------------------------

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(MonitorIntervalMs, token);

                    // Random disconnection simulation
                    if (IsConnected && _rand.NextDouble() < RandomDisconnectChance)
                    {
                        IsConnected = false;
                        _logger?.LogWarning("MockOutputDevice: RANDOMLY DISCONNECTED");

                        DeviceDisconnected?.Invoke(true);

                        // Begin reconnect loop
                        _ = ReconnectLoopAsync(token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal during shutdown
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MockOutputDevice: Error in monitor loop");
            }
        }

        // --------------------------------------------------------------------
        //  RECONNECT LOOP – keeps trying to reconnect until success
        // --------------------------------------------------------------------

        private async Task ReconnectLoopAsync(CancellationToken token)
        {
            try
            {
                var disconnectStart = DateTime.UtcNow;

                while (!IsConnected && !token.IsCancellationRequested)
                {
                    await Task.Delay(ReconnectAttemptIntervalMs, token);

                    // Enforce minimum downtime
                    if ((DateTime.UtcNow - disconnectStart).TotalMilliseconds < MinDisconnectDurationMs)
                    {
                        _logger?.LogInformation("MockOutputDevice: Waiting before reconnection allowed...");
                        continue;
                    }

                    // Simulate network delay + successful reconnect
                    _logger?.LogInformation("MockOutputDevice: Attempting to reconnect...");
                    await Task.Delay(250, token);

                    IsConnected = true;
                    _logger?.LogInformation("MockOutputDevice: Reconnected");

                    DeviceDisconnected?.Invoke(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "MockOutputDevice: Error in reconnect loop");
            }
        }
    }
}
