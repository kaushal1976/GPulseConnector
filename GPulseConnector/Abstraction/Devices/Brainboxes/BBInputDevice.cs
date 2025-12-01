using Brainboxes.IO;
using GPulseConnector.Options;
using Microsoft.Extensions.Options;
using GPulseConnector.Abstraction.Interfaces;
using System.Collections.ObjectModel;

namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public class BBInputDevice : IInputDevice
    {
        private readonly DeviceOptions _options;
        private readonly ILogger<BBInputDevice> _logger;
        private EDDevice? _device;

        public BBInputDevice(IOptions<DeviceOptions> options, ILogger<BBInputDevice> logger )
        {
            _options = options.Value;
            _logger = logger;
            _device = _device = new ED516(new TCPConnection(_options.InputDevices.IpAddress));
        }

        public event Action<IReadOnlyList<bool>>? InputsChanged;
        public event Action<bool>? DeviceDisconnected;


        public Task ConnectAsync(CancellationToken token = default)
        {
            if (_device is null)
            {
                _device = new ED516(new TCPConnection(_options.InputDevices.IpAddress));
            }
            else if (!_device.IsConnected)
            {
                try
                {
                    _device.Connect();
                    _logger.LogInformation("Reconnected to Brainboxes device at {IP}", _options.InputDevices.IpAddress);
                    return Task.FromResult(true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reconnect to Brainboxes device at {IP}", _options.InputDevices.IpAddress);
                    return Task.FromResult(false);
                }
            }
            else
            {
                _logger.LogInformation("Already connected to Brainboxes device at {IP}", _options.InputDevices.IpAddress);
                DeviceDisconnected?.Invoke(_device.IsConnected);
            }

            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<bool>> ReadInputsAsync(CancellationToken token = default)
        {
            if (_device is null || !_device.IsConnected)
            {
                _logger.LogWarning("Device not connected. Attempting to connect...");
                await ConnectAsync(token);
            }

            
            return _device?.Inputs
               .AsIOList()
               .Select(i => i.Value == 1)
               .ToList()
               .AsReadOnly() 
            ?? new ReadOnlyCollection<bool>(Array.Empty<bool>());
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            if (_device is null || !_device.IsConnected)
            {
                _logger.LogWarning("Device not connected. Attempting to connect...");
                await ConnectAsync(cancellationToken);
            }

            // Subscribe to events
            _device.IOLineChanged += (line, dev, changeType) =>
            {
                var inputs = _device.Inputs
                                    .AsIOList()
                                    .Select(i => i.Value == 1)
                                    .ToList()
                                    .AsReadOnly();

                if (inputs != null)
                    InputsChanged?.Invoke(inputs);
            };
            // Subscribe to device status changes
            _device.DeviceStatusChangedEvent += async (dev, property, newValue) =>
            {
                _logger.LogInformation("Device status changed: {Property} = {NewValue}", property, newValue);
                if ((property.Equals("IsAvailable")) && (newValue))
                {
                    _device.Connect();
                    _logger.LogInformation("Reconnected to Brainboxes device at {IP}", _options.InputDevices.IpAddress);
                }
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                _device.IOLineChanged -= (line, dev, changeType) =>
                    {
                        var inputs = _device?.Inputs
                                            .AsIOList()
                                            .Select(i => i.Value == 1)
                                            .ToList()
                                            .AsReadOnly();

                        if (inputs != null)
                            InputsChanged?.Invoke(inputs);
                    };

                _device.DeviceStatusChangedEvent -= async (dev, property, newValue) =>
                {
                    _logger.LogInformation("Device status changed: {Property} = {NewValue}", property, newValue);
                    if ((property.Equals("IsAvailable")) && (newValue))
                    {
                        _device.Connect();
                        _logger.LogInformation("Reconnected to Brainboxes device at {IP}", _options.InputDevices.IpAddress);
                    }
                };

                _logger.LogInformation("Stopped monitoring Brainboxes device at {IP}", _options.InputDevices.IpAddress);
            }
        }

        private void OnLineChanged(int line, object sender, bool newValue)
        {
            // Use _device directly
            var inputs = _device?.Inputs
                                .AsIOList()
                                .Select(i => i.Value == 1)
                                .ToList()
                                .AsReadOnly();

            if (inputs != null)
            {
                InputsChanged?.Invoke(inputs);
            }
        }

    }
}
