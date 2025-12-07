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
        private readonly EDDevice _device;
        private readonly BrainboxConnectionHandler _conn;

        private readonly int _numInputs;
        private bool _disposed;

        public event Action<IReadOnlyList<bool>>? InputsChanged;
        public event Action<bool>? DeviceDisconnected;

        public bool IsConnected => _conn.IsConnected;
        public bool IsAvailable => _conn.IsAvailable;

        public BrainboxInputDevice(
            IOptions<DeviceOptions> options,
            ILogger<BrainboxInputDevice> logger,
            int numInputs = 16,
            EDDevice? deviceOverride = null)
        {
            _logger = logger;
            _options = options.Value;
            _numInputs = numInputs;

            _device = deviceOverride ?? new ED516(new TCPConnection(_options.InputDevices.IpAddress));

            _conn = new BrainboxConnectionHandler(_device, _options.InputDevices.IpAddress, logger);
            _conn.ConnectionChanged += HandleConnectionChanged;

            AttachDeviceEvents();
        }

        // --------------------------------------------------------------------
        // CONNECTION / MONITORING
        // --------------------------------------------------------------------

        public Task ConnectAsync(CancellationToken token = default)
            => _conn.ConnectAsync(token); // helper auto-starts reconnect loop

        public Task StartMonitoringAsync(CancellationToken token)
            => ConnectAsync(token); // same behavior

        private void HandleConnectionChanged(bool connected, bool available)
        {
            if (!connected || !available)
            {
                DeviceDisconnected?.Invoke(true);
                _logger.LogWarning("Input device @{Ip} disconnected.", _options.InputDevices.IpAddress);
                return;
            }

            _logger.LogInformation("Input device @{Ip} connected.", _options.InputDevices.IpAddress);
            TriggerInputsChangedSafe();
        }

        // --------------------------------------------------------------------
        // INPUT READING
        // --------------------------------------------------------------------

        public async Task<IReadOnlyList<bool>> ReadInputsAsync(CancellationToken token = default)
        {
            if (!IsConnected || !IsAvailable)
                await _conn.EnsureConnectedAsync(token); // helper auto-starts reconnect if needed

            try
            {
                var list = _device.Inputs.AsIOList();

                if (list == null)
                    return SafeDefaults();

                return list
                    .Select(i => i.Value == 1)
                    .ToList()
                    .AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read inputs @{Ip}", _options.InputDevices.IpAddress);
                return SafeDefaults();
            }
        }

        private IReadOnlyList<bool> SafeDefaults()
            => Enumerable.Repeat(false, _numInputs).ToList().AsReadOnly();

        // --------------------------------------------------------------------
        // EVENT HANDLING
        // --------------------------------------------------------------------

        private void AttachDeviceEvents()
        {
            _device.IOLineChanged += (_, __, ___) => TriggerInputsChangedSafe();
        }

        private void TriggerInputsChangedSafe()
        {
            if (!IsConnected)
                return;

            try
            {
                var values = _device.Inputs.AsIOList()
                    .Select(i => i.Value == 1)
                    .ToList()
                    .AsReadOnly();

                Task.Run(() => InputsChanged?.Invoke(values));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing input change @{Ip}", _options.InputDevices.IpAddress);
            }
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
    }
}
