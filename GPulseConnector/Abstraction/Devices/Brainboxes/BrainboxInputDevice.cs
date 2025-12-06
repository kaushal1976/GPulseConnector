using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Brainboxes.IO;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public class BrainboxInputDevice : IInputDevice, IDisposable
    {
        private readonly ILogger<BrainboxInputDevice> _logger;
        private readonly DeviceOptions _options;

        private readonly int _numInputs;
        private readonly CancellationTokenSource _lifetimeCts = new();

        private readonly object _stateLock = new();
        private bool _connected;
        private bool _available;

        private volatile bool _isDisposed;

        private EDDevice _device;

        private Task? _reconnectTask;
        private readonly object _reconnectLock = new();

        public event Action<IReadOnlyList<bool>>? InputsChanged;
        public event Action<bool>? DeviceDisconnected;

        // Public read-only state
        public bool IsConnected { get { lock (_stateLock) return _connected; } }
        public bool IsAvailable { get { lock (_stateLock) return _available; } }

        public int ReconnectIntervalMs { get; set; } = 1000;

        public BrainboxInputDevice(
            IOptions<DeviceOptions> options,
            ILogger<BrainboxInputDevice> logger,
            int numInputs = 16,
            EDDevice? device = null)
        {
            _logger = logger;
            _options = options.Value;
            _numInputs = numInputs;

            _device = device ?? new ED516(new TCPConnection(_options.InputDevices.IpAddress));

            AttachDeviceEvents();
        }

        // --------------------------------------------------------------------
        // PUBLIC API
        // --------------------------------------------------------------------

        public async Task ConnectAsync(CancellationToken token = default)
        {
            await TryConnectAsync(token);

            // If connect fails silently, reconnect loop will fix it
            EnsureReconnectLoopRunning();
        }

        public async Task StartMonitoringAsync(CancellationToken token)
        {
            await ConnectAsync(token);
        }

        public async Task<IReadOnlyList<bool>> ReadInputsAsync(CancellationToken token = default)
        {
            await EnsureConnectedAsync(token);

            var values = _device.Inputs
                .AsIOList()
                .Select(i => i.Value == 1)
                .ToList();

            return values.AsReadOnly();
        }

        // --------------------------------------------------------------------
        // DEVICE CONNECT / DISCONNECT
        // --------------------------------------------------------------------

        private async Task TryConnectAsync(CancellationToken token)
        {
            if (IsConnected)
                return;
                
            try
            {
                await Task.Run(() => _device.Connect(), token);

                lock (_stateLock)
                {
                    _connected = true;
                    _available = true;
                }

                _logger.LogInformation($"Input Device @{ _options.InputDevices.IpAddress }: Connected successfully");
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    _connected = false;
                    _available = false;
                }

                _logger.LogWarning(ex,
                    $"Input Device @{ _options.InputDevices.IpAddress } connection failed");

                // reconnect loop will handle retry
            }
        }

        private void EnsureReconnectLoopRunning()
        {
            lock (_reconnectLock)
            {
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = ReconnectWorkerAsync(_lifetimeCts.Token);
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (!IsConnected)
                await TryConnectAsync(token);
        }

        // --------------------------------------------------------------------
        // RECONNECT LOOP
        // --------------------------------------------------------------------

        private async Task ReconnectWorkerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!IsConnected)
                {
                    _logger.LogInformation($"Input Device @{_options.InputDevices.IpAddress} reconnect attempt...");
                    await TryConnectAsync(token);
                }

                await Task.Delay(ReconnectIntervalMs, token);
            }
        }

        // --------------------------------------------------------------------
        // EVENT HANDLING
        // --------------------------------------------------------------------

        private void AttachDeviceEvents()
        {
            _device.IOLineChanged += (line, dev, type) => HandleInputsChanged();
            _device.DeviceStatusChangedEvent += HandleDeviceStatusChanged;
        }

        private void HandleInputsChanged()
        {
            if (!IsConnected)
                return;

            try
            {
                var values = _device.Inputs
                    .AsIOList()
                    .Select(i => i.Value == 1)
                    .ToList()
                    .AsReadOnly();

                Task.Run(() => InputsChanged?.Invoke(values));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading inputs @{IpAddress}", _options.InputDevices.IpAddress);
            }
        }

        private void HandleDeviceStatusChanged(
            IDevice<IConnection, IIOProtocol> device,
            string property,
            bool newValue)
        {
            if (device != _device)
                return;

            switch (property)
            {
                case "IsConnected":
                    UpdateConnectionState(newValue);
                    break;

                case "IsAvailable":
                    UpdateAvailabilityState(newValue);
                    break;

                default:
                    _logger.LogDebug($"Unknown device status: {property}={newValue} @{_options.InputDevices.IpAddress}");
                    break;
            }
        }

        private void UpdateConnectionState(bool isNowConnected)
        {
            bool wasConnected;

            lock (_stateLock)
            {
                wasConnected = _connected;
                _connected = isNowConnected;
            }

            if (isNowConnected)
            {
                // Do NOT log again — ConnectAsync already logs this
            }
            else
            {
                _logger.LogWarning($"Input Device @{_options.InputDevices.IpAddress} lost connection");
                DeviceDisconnected?.Invoke(true);
                EnsureReconnectLoopRunning();
            }
        }

        private void UpdateAvailabilityState(bool available)
        {
            lock (_stateLock)
            {
                _available = available;

                if (!available)
                    _connected = false;
            }

            if (!available)
            {
                _logger.LogWarning($"Input Device @{_options.InputDevices.IpAddress} became unavailable");
                DeviceDisconnected?.Invoke(true);
                EnsureReconnectLoopRunning();
            }
        }

        // --------------------------------------------------------------------
        // DISPOSAL
        // --------------------------------------------------------------------

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _lifetimeCts.Cancel();

            try { _device?.Dispose(); } catch { }
        }
    }
}
