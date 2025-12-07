using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Brainboxes.IO;
using GPulseConnector.Options;
using Microsoft.Extensions.Logging;

namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public class BrainboxConnectionHandler : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _ip;
        private readonly EDDevice _device;

        private readonly object _lock = new();
        private bool _connected;
        private bool _available;

        private readonly CancellationTokenSource _lifetimeCts = new();
        private Task? _reconnectTask;

        public event Action<bool, bool>? ConnectionChanged;

        public bool IsConnected
        {
            get { lock (_lock) return _connected; }
            private set { lock (_lock) _connected = value; }
        }

        public bool IsAvailable
        {
            get { lock (_lock) return _available; }
            private set { lock (_lock) _available = value; }
        }

        public int ReconnectIntervalMs { get; set; } = 2000;

        public EDDevice Device => _device;

        public BrainboxConnectionHandler(EDDevice device, string ip, ILogger logger)
        {
            _ip = ip;
            _logger = logger;
            _device = device;

            _device.DeviceStatusChangedEvent += OnDeviceStatusChanged;
        }

        // --------------------------------------------------------------------
        // PUBLIC API
        // --------------------------------------------------------------------

        public async Task ConnectAsync(CancellationToken token)
        {
            bool connected = await TryConnectAsync(token);

            if (!connected)
            {
                _logger.LogWarning("Device @{Ip} failed to connect, starting reconnect loop.", _ip);
            }

            EnsureReconnectLoopRunning();
        }

        public async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (!IsConnected)
                await TryConnectAsync(token);
        }

        // --------------------------------------------------------------------
        // INTERNAL CONNECT LOGIC
        // --------------------------------------------------------------------

        private async Task<bool> TryConnectAsync(CancellationToken token)
        {
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _device.Connect();
                        IsConnected = true;
                        IsAvailable = true;

                        _logger.LogInformation("Device @{Ip} connected.", _ip);
                        ConnectionChanged?.Invoke(true, true);
                    }
                    catch (SocketException ex)
                    {
                        _logger.LogWarning("Socket connect failed @{Ip}: {Message}", _ip, ex.Message);
                        IsConnected = false;
                        IsAvailable = false;
                    }
                }, token);

                return IsConnected;
            }
            catch
            {
                IsConnected = false;
                IsAvailable = false;
                return false;
            }
        }

        // --------------------------------------------------------------------
        // RECONNECT LOOP
        // --------------------------------------------------------------------

        private void EnsureReconnectLoopRunning()
        {
            if (_reconnectTask == null || _reconnectTask.IsCompleted)
            {
                _reconnectTask = Task.Run(() => ReconnectLoopAsync(_lifetimeCts.Token));
            }
        }

        private async Task ReconnectLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (!IsConnected)
                {
                    _logger.LogWarning("Device @{Ip} attempting reconnect...", _ip);

                    bool connected = await TryConnectAsync(token);

                    if (connected)
                    {
                        _logger.LogInformation("Device @{Ip} reconnected.", _ip);
                    }
                }

                await Task.Delay(ReconnectIntervalMs, token);
            }
        }

        // --------------------------------------------------------------------
        // EVENT PROCESSING
        // --------------------------------------------------------------------

        private void OnDeviceStatusChanged(IDevice<IConnection, IIOProtocol> dev, string property, bool newValue)
        {
            if (dev != _device) return;

            switch (property)
            {
                case "IsConnected":
                    IsConnected = newValue;
                    break;

                case "IsAvailable":
                    IsAvailable = newValue;
                    if (!newValue) IsConnected = false;
                    break;
            }

            ConnectionChanged?.Invoke(IsConnected, IsAvailable);

            if (!newValue)
                EnsureReconnectLoopRunning();
        }

        // --------------------------------------------------------------------
        // DISPOSAL
        // --------------------------------------------------------------------

        public void Dispose()
        {
            _lifetimeCts.Cancel();
            try { _device?.Dispose(); } catch { }
        }
    }
}
