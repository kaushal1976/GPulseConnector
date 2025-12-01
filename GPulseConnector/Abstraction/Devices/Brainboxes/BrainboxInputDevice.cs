using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly int _numInputs;

        private readonly object _connLock = new();
        private bool _isConnected;
        private bool _isAvailable;

        private readonly CancellationTokenSource _cts = new();
        private Task? _reconnectTask;
        private readonly object _reconnectLock = new();

        private EDDevice _device;

        public int ReconnectAttemptIntervalMs { get; set; } = 500;

        public event Action<IReadOnlyList<bool>>? InputsChanged;
        public event Action<bool>? DeviceDisconnected;

        public bool IsConnected
        {
            get { lock (_connLock) return _isConnected; }
            private set { lock (_connLock) _isConnected = value; }
        }

        public bool IsAvailable
        {
            get { lock (_connLock) return _isAvailable; }
            private set { lock (_connLock) _isAvailable = value; }
        }

        public BrainboxInputDevice(
            IOptions<DeviceOptions> options,
            ILogger<BrainboxInputDevice> logger,
            int numInputs = 16,
            EDDevice? device = null)
        {
            _logger = logger;
            _numInputs = numInputs;
            _device = device ?? new ED516(new TCPConnection(options.Value.InputDevices.IpAddress));
        }

        public Task ConnectAsync(CancellationToken token = default) => Task.CompletedTask;

        public async Task StartMonitoringAsync(CancellationToken token)
        {
            await ConnectDeviceAsync(token);
            SubscribeToDeviceEvents();
        }

        public async Task<IReadOnlyList<bool>> ReadInputsAsync(CancellationToken token = default)
        {
            await EnsureConnectedAsync(token);

            return _device.Inputs
                .AsIOList()
                .Select(i => i.Value == 1)
                .ToList()
                .AsReadOnly();
        }

        private void SubscribeToDeviceEvents()
        {
            _device.IOLineChanged += OnDeviceInputChanged;
            _device.DeviceStatusChangedEvent += OnDeviceStatusChanged;
        }

        private void OnDeviceInputChanged(IOLine line, EDDevice dev, IOChangeTypes changeType)
        {
            var values = _device.Inputs
                .AsIOList()
                .Select(i => i.Value == 1)
                .ToList()
                .AsReadOnly();

            Task.Run(() => InputsChanged?.Invoke(values));
        }

        private void OnDeviceStatusChanged(IDevice<IConnection, IIOProtocol> device, string property, bool newValue)
        {
            if (device != _device) return;

            switch (property)
            {
                case "IsConnected":
                    IsConnected = newValue;
                    if (!newValue) TriggerReconnectLoop();
                    break;

                case "IsAvailable":
                    IsAvailable = newValue;
                    if (!newValue)
                    {
                        IsConnected = false;
                        TriggerReconnectLoop();
                    }
                    break;

                default:
                    _logger.LogWarning($"Unknown device property changed: {property}");
                    break;
            }
        }

        private void TriggerReconnectLoop()
        {
            DeviceDisconnected?.Invoke(false);

            lock (_reconnectLock)
            {
                if (_reconnectTask == null || _reconnectTask.IsCompleted)
                    _reconnectTask = AttemptReconnectLoopAsync(_cts.Token);
            }
        }

        private async Task ConnectDeviceAsync(CancellationToken token)
        {
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    _device.Connect(); // synchronous connect
                    lock (_connLock)
                    {
                        _isConnected = true;
                        _isAvailable = true;
                    }
                    _logger.LogInformation("Device connected successfully");
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    lock (_connLock)
                    {
                        _isConnected = false;
                        _isAvailable = false;
                    }
                    _logger.LogWarning(ex, "Device connection failed: network unreachable or timeout");
                }
                catch (Exception ex)
                {
                    lock (_connLock)
                    {
                        _isConnected = false;
                        _isAvailable = false;
                    }
                    _logger.LogError(ex, "Unexpected device connection error");
                }
            }, token);
        }

        private async Task AttemptReconnectLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !IsConnected)
            {
                _logger.LogInformation("Attempting immediate reconnect...");
                await ConnectDeviceAsync(token);

                if (!IsConnected)
                {
                    await Task.Delay(ReconnectAttemptIntervalMs, token);
                }
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (!IsConnected)
                await AttemptReconnectLoopAsync(token);
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
