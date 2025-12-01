using Brainboxes.IO;
using GPulseConnector.Options;
using Microsoft.Extensions.Options;
using GPulseConnector.Abstraction.Interfaces;

namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public class BBOutputDevice : IOutputDevice
    {
        private readonly DeviceOptions _options;
        private readonly ILogger<BBOutputDevice> _logger;
        private EDDevice? _device;
        private readonly bool[] _outputs;
        public int OutputCount => _outputs.Length;

        public BBOutputDevice(IOptions<DeviceOptions> options, ILogger<BBOutputDevice> logger, int outputCount = 16, BBStatusMonitor? monitor = null)
        {
            _options = options.Value;
            _logger = logger;
            _outputs = new bool[outputCount];
            _device = new ED527(new TCPConnection(_options.OutputDevices.IpAddress));
            _device.Connect();

        }

        public event Action<int, bool>? OutputChanged;
        public event Action<bool>? DeviceDisconnected;

        public async Task ConnectAsync(CancellationToken token = default)
        {
            try
            {
                if (_device == null)
                {
                    _device = new ED527(new TCPConnection(_options.OutputDevices.IpAddress));
                }
                else if (!_device.IsConnected)
                {
                    _device.Connect();
                    _logger.LogInformation("Reconnected to Brainboxes device at {IP}", _options.OutputDevices.IpAddress);
                    DeviceDisconnected?.Invoke(_device.IsConnected);
                    return;
                }
                else
                {
                    _logger.LogInformation("Already connected to Brainboxes device at {IP}", _options.OutputDevices.IpAddress);
                    DeviceDisconnected?.Invoke(_device.IsConnected);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Brainboxes device at {IP}", _options.OutputDevices.IpAddress);
                throw;
            }
            _device?.Connect();
            _logger.LogInformation("Connected Brainboxes device at {IP}", _options.OutputDevices.IpAddress);
        
        }

        public async Task DisconnectAsync(CancellationToken token = default)
        {
            if (_device != null && _device.IsConnected)
            {
                _device.Disconnect();
                _logger.LogInformation("Disconnected Brainboxes device at {IP}", _options.OutputDevices.IpAddress);
            }

            await Task.CompletedTask;
        }

        public async Task<IReadOnlyList<bool>> ReadOutputsAsync(CancellationToken token = default)
        {
            await EnsureConnectedAsync(token).ConfigureAwait(false);

            return _device?.Outputs
                           .AsIOList()
                           .Select(i => i.Value == 1)
                           .ToList()
                           .AsReadOnly() 
                   ?? Array.Empty<bool>().ToList().AsReadOnly();
        }

        public async Task SetOutputAsync(int outputIndex, bool value, CancellationToken token = default)
        {
            await EnsureConnectedAsync(token).ConfigureAwait(false);

            if (outputIndex < 0 || outputIndex >= _outputs.Length)
                throw new ArgumentOutOfRangeException(nameof(outputIndex), "Invalid output index");

            _outputs[outputIndex] = value;
            _device!.Outputs[outputIndex].Value = value ? 1 : 0;

            OutputChanged?.Invoke(outputIndex, value);
        }

        public async Task SetOutputsAsync(IReadOnlyList<bool> outputs, CancellationToken token = default)
        {
            await EnsureConnectedAsync(token).ConfigureAwait(false);

            if (outputs.Count != _outputs.Length)
                throw new ArgumentException("Output count mismatch", nameof(outputs));

            for (int i = 0; i < outputs.Count; i++)
            {
                _outputs[i] = outputs[i];
                _device!.Outputs[i].Value = outputs[i] ? 1 : 0;
                OutputChanged?.Invoke(i, outputs[i]);
            }
        }

        private async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (_device == null || !_device.IsConnected)
            {
                await ConnectAsync(token).ConfigureAwait(false);

            }
            
        }
        
    }
}
