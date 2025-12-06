using GPulseConnector.Abstraction.Interfaces;
using Microsoft.Extensions.Logging;
using Brainboxes.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using GPulseConnector.Options;

namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public class BrainboxOutputDevice : IOutputDevice, IDisposable
    {
        private readonly ILogger<BrainboxOutputDevice>? _logger;
        private readonly IOptions<DeviceOptions> _options;
        private readonly EDDevice _device;
        private readonly int _outputCount;

        private readonly object _connLock = new();
        private CancellationTokenSource? _monitorCts;
        private Task? _monitorTask;

        private bool _isConnected = false;

        // Reconnect settings
        private const int ReconnectIntervalMs = 5000;

        public bool IsConnected
        {
            get { lock (_connLock) return _isConnected; }
            private set { lock (_connLock) _isConnected = value; }
        }

        public int OutputCount => _outputCount;

        public event Action<int, bool>? OutputChanged;
        public event Action<bool>? DeviceDisconnected;

        public BrainboxOutputDevice(IOptions<DeviceOptions> options, ILogger<BrainboxOutputDevice>? logger = null, int outputCount = 16)
        {
            _device = new ED527(new TCPConnection(options.Value.OutputDevices.IpAddress));
            _logger = logger;
            _outputCount = outputCount;
            _options = options;

            // Hook device events
            _device.IOLineChanged += OnDeviceOutputChanged;
            _device.DeviceStatusChangedEvent += OnDeviceStatusChanged;
        }

        private void OnDeviceOutputChanged(IOLine line, EDDevice dev, IOChangeTypes changeType)
        {
            int idx = _device.Outputs
                .Select((o, i) => new { o, i })
                .FirstOrDefault(x => x.o == line)?.i ?? -1;

            if (idx >= 0)
                OutputChanged?.Invoke(idx, line.Value == 1);
        }

        private void OnDeviceStatusChanged(IDevice<IConnection, IIOProtocol> device, string property, bool newValue)
        {
            if (device != _device) return;

            switch (property)
            {
                case "IsConnected":
                    IsConnected = newValue;
                    if (!newValue) DeviceDisconnected?.Invoke(true);
                    break;
                case "IsAvailable":
                    if (!newValue)
                    {
                        IsConnected = false;
                        DeviceDisconnected?.Invoke(true);
                    }
                    break;
            }
        }

        public async Task ConnectAsync(CancellationToken token = default)
        {
            lock (_connLock)
            {
                if (IsConnected) return;
            }

            try
            {
                _device.Connect(); // try immediately
                lock (_connLock) IsConnected = true;

                DeviceDisconnected?.Invoke(false);
                _logger?.LogInformation($"Output Device @{_options.Value.OutputDevices.IpAddress}: Connected successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"Initial connect to {_options.Value.OutputDevices.IpAddress} failed");
            }

            // Then start monitoring loop
            StartReconnectMonitor(token);
        }

        private void StartReconnectMonitor(CancellationToken token)
        {
            _monitorCts?.Cancel();
            _monitorCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            token = _monitorCts.Token;

            _monitorTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (!IsConnected)
                        {
                            _device.Connect();
                            lock (_connLock) IsConnected = true;

                            DeviceDisconnected?.Invoke(false);
                        }
                    }
                    catch
                    {
                        lock (_connLock) IsConnected = false;
                        DeviceDisconnected?.Invoke(true);
                    }

                    await Task.Delay(ReconnectIntervalMs, token);
                }
            }, token);
        }


        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _monitorCts?.Cancel();
            lock (_connLock)
            {
                if (!IsConnected) return Task.CompletedTask;

                try { _device.Disconnect(); }
                catch { /* swallow */ }

                IsConnected = false;
                DeviceDisconnected?.Invoke(true);
                _logger?.LogInformation($"Output Device @{_options.Value.OutputDevices.IpAddress}: Disconnected");
            }
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<bool>> ReadOutputsAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                _logger?.LogWarning($"Output Device @{_options.Value.OutputDevices.IpAddress}: ReadOutputsAsync called while device not connected.");
                return Enumerable.Repeat(false, _outputCount).ToList();
            }

            return await Task.FromResult(_device.Outputs
                .Select(o => o.Value == 1)
                .ToList()
                .AsReadOnly());
        }

        public Task SetOutputAsync(int index, bool value, CancellationToken cancellationToken = default)
        {
            lock (_connLock)
            {
                if (!IsConnected)
                    throw new InvalidOperationException($"Output Device @{_options.Value.OutputDevices.IpAddress} is not connected");

                _device.Outputs[index].Value = value ? 1 : 0;
            }

            OutputChanged?.Invoke(index, value);
            return Task.CompletedTask;
        }

        public Task SetOutputsAsync(IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
        {
            lock (_connLock)
            {
                if (!IsConnected)
                    throw new InvalidOperationException($"Output Device @{_options.Value.OutputDevices.IpAddress} is not connected");

                for (int i = 0; i < values.Count; i++)
                    _device.Outputs[i].Value = values[i] ? 1 : 0;
            }

            for (int i = 0; i < values.Count; i++)
                OutputChanged?.Invoke(i, values[i]);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _monitorCts?.Cancel();
            try { _monitorTask?.Wait(200); } catch { /* swallow */ }
            _monitorCts?.Dispose();
        }
    }
}
