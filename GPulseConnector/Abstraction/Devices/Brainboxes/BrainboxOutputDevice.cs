using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Brainboxes.IO;
using GPulseConnector.Abstraction.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GPulseConnector.Options;

namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public class BrainboxOutputDevice : IOutputDevice, IDisposable
    {
        private readonly ILogger<BrainboxOutputDevice>? _logger;
        private readonly DeviceOptions _options;
        private readonly EDDevice _device;
        private readonly BrainboxConnectionHandler _conn;

        private readonly int _outputCount;
        private bool _disposed;

        public event Action<int, bool>? OutputChanged;
        public event Action<bool>? DeviceDisconnected;

        public bool IsConnected => _conn.IsConnected;
        public int OutputCount => _outputCount;

        public BrainboxOutputDevice(
            IOptions<DeviceOptions> options,
            ILogger<BrainboxOutputDevice>? logger = null,
            int outputCount = 16,
            EDDevice? deviceOverride = null)
        {
            _options = options.Value;
            _logger = logger;

            _outputCount = outputCount;

            _device = deviceOverride ?? new ED527(new TCPConnection(options.Value.OutputDevices.IpAddress));

            _conn = new BrainboxConnectionHandler(_device, _options.OutputDevices.IpAddress, logger!);
            _conn.ConnectionChanged += HandleConnectionChanged;
            _device.IOLineChanged += OnDeviceOutputChanged;
        }

        // --------------------------------------------------------------------
        // CONNECTION
        // --------------------------------------------------------------------

        public Task ConnectAsync(CancellationToken token = default)
            => _conn.ConnectAsync(token);

        private void HandleConnectionChanged(bool connected, bool available)
        {
            if (!connected || !available)
            {
                DeviceDisconnected?.Invoke(true);
                _logger?.LogWarning("Output device @{Ip} disconnected.", _options.OutputDevices.IpAddress);
            }
            else
            {
                DeviceDisconnected?.Invoke(false);
                _logger?.LogInformation("Output device @{Ip} connected.", _options.OutputDevices.IpAddress);
            }
        }

        // --------------------------------------------------------------------
        // OUTPUT OPERATIONS
        // --------------------------------------------------------------------

        public async Task<IReadOnlyList<bool>> ReadOutputsAsync(CancellationToken token = default)
        {
            if (!IsConnected)
            {
                _logger?.LogWarning("ReadOutputsAsync called while device not connected @{Ip}", _options.OutputDevices.IpAddress);
                return Enumerable.Repeat(false, _outputCount).ToList().AsReadOnly();
            }

            await _conn.EnsureConnectedAsync(token);

            return _device.Outputs
                .Select(o => o.Value == 1)
                .ToList()
                .AsReadOnly();
        }

        public Task SetOutputAsync(int index, bool value, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException($"Output device @{_options.OutputDevices.IpAddress} not connected");

            _device.Outputs[index].Value = value ? 1 : 0;
            OutputChanged?.Invoke(index, value);

            return Task.CompletedTask;
        }

        public Task SetOutputsAsync(IReadOnlyList<bool> values, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                throw new InvalidOperationException($"Output device @{_options.OutputDevices.IpAddress} not connected");

            for (int i = 0; i < values.Count; i++)
                _device.Outputs[i].Value = values[i] ? 1 : 0;

            for (int i = 0; i < values.Count; i++)
                OutputChanged?.Invoke(i, values[i]);

            return Task.CompletedTask;
        }

        // --------------------------------------------------------------------
        // EVENT HANDLING
        // --------------------------------------------------------------------

        private void OnDeviceOutputChanged(IOLine line, EDDevice dev, IOChangeTypes changeType)
        {
            int idx = _device.Outputs
                .Select((o, i) => new { o, i })
                .FirstOrDefault(x => x.o == line)?.i ?? -1;

            if (idx >= 0)
                OutputChanged?.Invoke(idx, line.Value == 1);
        }

        // --------------------------------------------------------------------
        // DISPOSAL
        // --------------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _conn.Dispose();
            _device?.Dispose();
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
