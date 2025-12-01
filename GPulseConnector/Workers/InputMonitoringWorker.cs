using GPulseConnector.Abstraction.Factories;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace GPulseConnector.Workers
{
    public class InputMonitoringWorker : BackgroundService
    {
        private readonly IInputDevice _device;
        private readonly Channel<IReadOnlyList<bool>> _channel;
        private readonly ILogger<InputMonitoringWorker> _logger;

        public InputMonitoringWorker(IInputDevice device, Channel<IReadOnlyList<bool>> channel, DeviceRecordFactory factory, ILogger<InputMonitoringWorker> logger)
        {
            _device = device;
            _channel = channel;
            _logger = logger;
        
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
        
            _logger.LogInformation("InputMonitoringWorker starting…");
            await ReadInputs(token);

            // Subscribe to device events
            _device.InputsChanged += OnInputsChanged;
            await _device.StartMonitoringAsync(token);
            await Task.Delay(Timeout.Infinite, token);
        }

        private void OnInputsChanged(IReadOnlyList<bool> inputs)
        {
            if (!_channel.Writer.TryWrite(inputs))
            {
                _logger.LogWarning("Dropped input snapshot because channel is full");
            }
        }

        private async Task ReadInputsLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token); // Polling interval
            }
        }

        private async Task ReadInputs (CancellationToken token)
        {
                try
                {
                    var inputs = await _device.ReadInputsAsync(token);
                    if (!_channel.Writer.TryWrite(inputs))
                    {
                        _logger.LogWarning("Dropped input snapshot because channel is full");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading inputs");
                }

        }
       
    }
}
