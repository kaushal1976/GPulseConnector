using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Services;

namespace GPulseConnector.Workers;

public class OutputUpdateWorker : BackgroundService
{
    private readonly IOutputDevice _device;
    private readonly IPatternMappingCache _cache;
    private readonly Blinker _blinker;
    private readonly ILogger<OutputUpdateWorker> _logger;

    private const int HelpSignalOutputIndex = 3;
    private const int HelpSignalBlinkMs = 125;


    public OutputUpdateWorker(
        IOutputDevice device, 
        IPatternMappingCache cache, 
        Blinker blinker, 
        ILogger<OutputUpdateWorker> logger) 
    {
        _device = device;
        _cache = cache;
        _blinker = blinker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Output Controller Starting..");

        // Read snapshot
        var current = await _device.ReadOutputsAsync(stoppingToken);    

        _logger.LogInformation("Current output is {Output0}, {Output1}, {Output2}", 
            current[0], current[1], current[2]);
        
        _device.DeviceDisconnected += (isConnected) => OnDeviceDisconnected(isConnected);
    }

    public async Task UpdateOutputAsync(PatternMapping mapping, CancellationToken stoppingToken)
    {
        if (mapping == null)
        {
            _logger.LogInformation("No patterns matched.");
            return;
        }

        try
        {
            _logger.LogInformation("InputStatus is {InputStatus}", mapping.InputStatus);

            IReadOnlyList<bool> finalValues = new List<bool> 
            { 
                mapping.OD0, mapping.OD1, mapping.OD2,
                false, false, false, false, false, 
                false, false, false, false, false, 
                false, false, false 
            };
            
            _logger.LogInformation("New OutputStatus is {OD0}, {OD1}, {OD2}", 
                mapping.OD0, mapping.OD1, mapping.OD2);

            // This returns immediately after cancelling previous blink and starting new one
            await _blinker.StartOrRestartAsync(
                finalValues, 
                async status =>
                {
                    try
                    {
                        await _device.SetOutputsAsync(status, stoppingToken);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogError(ex, "Error setting device outputs");
                    }
                }, 
                blinkIntervalMs: 125,
                blinkDurationMs: 5000, 
                stoppingToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateOutputAsync");
        }
    }

    public async Task CallingForHelpAsync(bool status, CancellationToken stoppingToken)
    {
        if (status)
        _logger.LogInformation("Operator is calling for help. Activating help signal.");

        try
        {
            await _device.SetOutputAsync(HelpSignalOutputIndex, status, stoppingToken);  

        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error in CallingForHelpAsync");
        }
    }

    public void OnDeviceDisconnected(bool isConnected)
    {
        _logger.LogWarning("Device disconnected: {IsConnected}", isConnected);
        // Add reconnection logic if needed
    }

    public override void Dispose()
    {
        _blinker?.Dispose();
        base.Dispose();
    }
}