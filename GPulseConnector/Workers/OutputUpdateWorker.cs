using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPulseConnector.Workers;
public class OutputUpdateWorker : BackgroundService
{
    private readonly IOutputDevice _device;
    private readonly IPatternMappingCache _cache;
    private readonly Blinker _blinker;
    private readonly ILogger<OutputUpdateWorker> _logger;

    public OutputUpdateWorker(IOutputDevice device, IPatternMappingCache cache, Blinker blinker, ILogger<OutputUpdateWorker> logger) 
    {
        _device = device;
        _cache = cache;
        _blinker = blinker;
        _logger = logger;

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _device.ConnectAsync(stoppingToken);
        _logger.LogInformation("Output Controller Starting..");

        // Read snapshot
        var current = await _device.ReadOutputsAsync(stoppingToken);    

        // Log them or process them
        _logger.LogInformation("Current output is {Output0}, {Output1}, {Output2}", current[0], current[1], current[2]);
        _device.DeviceDisconnected += (isConnected) => OnDeviceDisconnected(isConnected);
    }

    public async Task UpdateOutputAsync(PatternMapping mapping, CancellationToken stoppingToken)
    {

        if (mapping != null)
        {
            _logger.LogInformation("InputStatus is {InputStatus}", mapping.InputStatus);

            IReadOnlyList<bool> finalValues = new List<bool> { mapping.OD0, mapping.OD1, mapping.OD2,false,false,false,false,false,false,false,false,false,false,false,false,false };
            var currentStatus = await _device.ReadOutputsAsync(stoppingToken);
            
            _logger.LogInformation("New OutputStatus is {OD0}, {OD1}, {OD2}", mapping.OD0, mapping.OD1, mapping.OD2);

            Func<IReadOnlyList<bool>, Task> tickCallback = async status =>
            {
                await _device.SetOutputsAsync(status, stoppingToken);
            };

            await _blinker.StartOrRestartAsync(currentStatus, finalValues, tickCallback, blinkIntervalMs: 125,
                    blinkDurationMs: 5000, stoppingToken);

        }
        else
        {
            _logger.LogInformation("No patterns matched.");
        }
    }
    public async void OnDeviceDisconnected(bool isConnected)
    {
        _logger.LogWarning("Device disconnected unexpectedly!");
            
    }
    
}