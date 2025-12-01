using Brainboxes.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public class BBStatusMonitor : BackgroundService
    {
        private readonly EDDevice _device;
        private readonly ILogger<BBStatusMonitor> _logger;

        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(3);

        public event Action? DeviceConnected;
        public event Action? DeviceDisconnected;

        public BBStatusMonitor(EDDevice device, ILogger<BBStatusMonitor> logger)
        {
            _device = device;
            _logger = logger;

            // Subscribe to device internal events
            _device.DeviceStatusChangedEvent += OnDeviceStatusChanged;
        }

        private void OnDeviceStatusChanged(IDevice<IConnection, IIOProtocol> device, string property, bool newValue)
        {
            switch (_device.IsConnected)
            {
                case true:
                    _logger.LogInformation("Brainboxes connected.");
                    DeviceConnected?.Invoke();
                    break;

                case false:
                    _logger.LogWarning("Brainboxes disconnected.");
                    DeviceDisconnected?.Invoke();
                    break;

            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PerformHealthCheckAsync(stoppingToken);
                await Task.Delay(_healthCheckInterval, stoppingToken);
            }
        }

        private async Task PerformHealthCheckAsync(CancellationToken token)
        {
            try
            {
                if (!_device.IsConnected)
                {
                    _logger.LogWarning("Device not connected. Attempting reconnect...");

                    _device.Connect();

                    if (_device.IsConnected)
                    {
                        _logger.LogInformation("Reconnect successful.");
                        DeviceConnected?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check / reconnect failed.");
            }

            await Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            _device.DeviceStatusChangedEvent -= OnDeviceStatusChanged;
        }
    }
}
