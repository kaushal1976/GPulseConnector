using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using GPulseConnector.Abstraction.Interfaces;

public class InputMonitoringWorker : BackgroundService
{
    private readonly IInputDevice _device;
    private readonly Channel<IReadOnlyList<bool>> _channel;
    private readonly ILogger<InputMonitoringWorker> _logger;
    
    // Metrics
    private long _eventsReceived;
    private long _eventsDropped;
    private DateTime _lastEventTime = DateTime.MinValue;

    public InputMonitoringWorker(
        IInputDevice device, 
        Channel<IReadOnlyList<bool>> channel, 
        ILogger<InputMonitoringWorker> logger)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InputMonitoringWorker starting…");
        
        try
        {
            // Subscribe to input changes
            _device.InputsChanged += OnInputsChanged;
            
            // Start monitoring the device
            await _device.StartMonitoringAsync(stoppingToken);
            
            // Keep the service alive until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("InputMonitoringWorker stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InputMonitoringWorker encountered an unhandled error");
            throw; // Let the host handle fatal errors
        }
        finally
        {
            // Always unsubscribe to prevent memory leaks
            _device.InputsChanged -= OnInputsChanged;
            
            // Log final metrics
            _logger.LogInformation(
                "InputMonitoringWorker stopped. Events received: {Received}, dropped: {Dropped}, last event: {LastEvent}",
                _eventsReceived, _eventsDropped, _lastEventTime);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InputMonitoringWorker shutdown requested");
        
        // Signal completion to channel consumers
        _channel.Writer.Complete();
        
        await base.StopAsync(cancellationToken);
    }

    private void OnInputsChanged(IReadOnlyList<bool> inputs)
    {
        _lastEventTime = DateTime.UtcNow;
        Interlocked.Increment(ref _eventsReceived);
        
        // Try to write to channel - non-blocking
        if (_channel.Writer.TryWrite(inputs))
        {
            _logger.LogDebug("Input event written to channel. Total events: {Count}", _eventsReceived);
        }
        else
        {
            var dropped = Interlocked.Increment(ref _eventsDropped);
            _logger.LogWarning(
                "Dropped input snapshot because channel is full. Total dropped: {Dropped}", 
                dropped);
        }
    }
}