using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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

        private volatile bool _isDisposed = false;

        private readonly object _reconnectLock = new();
        private Task? _reconnectTask;

        private EDDevice _device;

        public event Action<IReadOnlyList<bool>>? InputsChanged;
        public event Action<bool>? DeviceDisconnected;

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
            EnsureReconnectLoopRunning();
        }

        public async Task StartMonitoringAsync(CancellationToken token)
        {
            await ConnectAsync(token);
        }

        public async Task<IReadOnlyList<bool>> ReadInputsAsync(CancellationToken token = default)
        {
            // If not connected, return a "safe" result (false for all inputs)
            if (!IsConnected || !IsAvailable)
            {
                return Enumerable.Repeat(false, _numInputs).ToList().AsReadOnly();
            }

            await EnsureConnectedAsync(token);

            try
            {
                var list = _device?.Inputs?.AsIOList();

                // Brainboxes sometimes returns null when reconnecting
                if (list == null)
                {
                    return Enumerable.Repeat(false, _numInputs).ToList().AsReadOnly();
                }

                var values = list
                    .Select(i => i.Value == 1)
                    .ToList();

                return values.AsReadOnly();
            }
            catch (Exception ex)
            {
                // Prevent Brainboxes crashes during reconnection
                _logger.LogWarning(ex, 
                    "Input read failed for @{Ip}, returning safe defaults",
                    _options.InputDevices.IpAddress);

                return Enumerable.Repeat(false, _numInputs).ToList().AsReadOnly();
            }
        }


        // --------------------------------------------------------------------
        // INTERNAL CONNECT LOGIC 
        // --------------------------------------------------------------------

        private async Task<bool> TryConnectAsync(CancellationToken token)
        {
            bool success = false;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _device.Connect();
                        success = true;
                    }
                    catch (SocketException ex)
                    {
                        _logger.LogWarning(
                            "Input Device @{Ip} connection failed: {Message}",
                            _options.InputDevices.IpAddress, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Unexpected error connecting to Input Device @{Ip}",
                            _options.InputDevices.IpAddress);
                    }
                }, token);

                lock (_stateLock)
                {
                    _connected = success;
                    _available = success;
                }

                if (success)
                {
                    _logger.LogInformation(
                        "Input Device @{Ip}: Connected successfully",
                        _options.InputDevices.IpAddress);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal connect error @{Ip}", _options.InputDevices.IpAddress);

                lock (_stateLock)
                {
                    _connected = false;
                    _available = false;
                }
            }

            return success;
        }

        private async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (!IsConnected)
                await TryConnectAsync(token);
        }

        // --------------------------------------------------------------------
        // RECONNECT LOOP
        // --------------------------------------------------------------------

        private void EnsureReconnectLoopRunning()
        {
            lock (_reconnectLock)
            {
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                {
                    _reconnectTask = Task.Run(() => ReconnectWorkerAsync(_lifetimeCts.Token));
                }
            }
        }

        private async Task ReconnectWorkerAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!IsConnected)
                {
                    _logger.LogInformation(
                        "Input Device @{Ip} attempting reconnection...",
                        _options.InputDevices.IpAddress);

                    bool connected = await TryConnectAsync(token);

                    if (connected)
                    {
                        _logger.LogInformation(
                            "Input Device @{Ip} reconnected.",
                            _options.InputDevices.IpAddress);

                        return; // END RECONNECT LOOP
                    }
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
                    _logger.LogDebug(
                        "Unknown device status: {Property}={Value} @{Ip}",
                        property, newValue, _options.InputDevices.IpAddress);
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

            if (isNowConnected && !wasConnected)
            {
                _logger.LogInformation(
                    "Input Device @{Ip} connected (event).",
                    _options.InputDevices.IpAddress);
            }
            else if (!isNowConnected && wasConnected)
            {
                _logger.LogWarning(
                    "Input Device @{Ip} lost connection.",
                    _options.InputDevices.IpAddress);

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
                _logger.LogWarning(
                    "Input Device @{Ip} became unavailable.",
                    _options.InputDevices.IpAddress);

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

            try { _device?.Dispose(); }
            catch { }
        }
    }
}
