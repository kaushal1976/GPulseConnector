using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Workers;
using Microsoft.Extensions.Logging.Abstractions;

namespace GPulseConnector.Tests;

public class InputMonitoringWorkerTests : IDisposable
    {
        private readonly Mock<IInputDevice> _mockDevice; // Fully qualified
        private readonly Channel<IReadOnlyList<bool>> _channel;
        private readonly Mock<ILogger<InputMonitoringWorker>> _mockLogger;
        private readonly InputMonitoringWorker _worker;
        private readonly CancellationTokenSource _cts;

        public InputMonitoringWorkerTests()
        {
            _mockDevice = new Mock<IInputDevice>();
            _channel = Channel.CreateBounded<IReadOnlyList<bool>>(10);
            _mockLogger = new Mock<ILogger<GInputMonitoringWorker>>();
            _cts = new CancellationTokenSource();
            
            _worker = new InputMonitoringWorker(
                _mockDevice.Object, 
                _channel, 
                _mockLogger.Object);
        }

    [Fact]
    public async Task ExecuteAsync_StartsMonitoring_AndSubscribesToEvents()
    {
        // Arrange
        var monitoringStarted = new TaskCompletionSource<bool>();
        
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => monitoringStarted.SetResult(true));

        // Act
        var executeTask = _worker.StartAsync(_cts.Token);
        await monitoringStarted.Task; // Wait for monitoring to start
        await Task.Delay(100); // Give event subscription time to complete
        _cts.Cancel();

        // Assert
        _mockDevice.Verify(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        // Verify the information log was called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("InputMonitoringWorker starting")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnInputsChanged_ReadsInputsAndWritesToChannel()
    {
        // Arrange
        var expectedInputs = new List<bool> { true, false, true }.AsReadOnly();
        var readInputsCalled = new TaskCompletionSource<bool>();
        
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockDevice.Setup(d => d.ReadInputsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInputs)
            .Callback(() => readInputsCalled.SetResult(true));

        // Act
        var startTask = _worker.StartAsync(_cts.Token);
        await Task.Delay(100); // Let the worker start
        
        // Trigger the InputsChanged event
        _mockDevice.Raise(d => d.InputsChanged += null, expectedInputs);
        
        // Wait for ReadInputsAsync to be called
        await readInputsCalled.Task;
        await Task.Delay(100); // Give time for channel write

        _cts.Cancel();

        // Assert
        _mockDevice.Verify(d => d.ReadInputsAsync(It.IsAny<CancellationToken>()), Times.Once);
        
        // Check if data was written to channel
        Assert.True(_channel.Reader.TryRead(out var result));
        Assert.Equal(expectedInputs, result);
    }

    [Fact]
    public async Task ReadInputs_WritesInputsToChannel_WhenSuccessful()
    {
        // Arrange
        var expectedInputs = new List<bool> { true, true, false }.AsReadOnly();
        
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockDevice.Setup(d => d.ReadInputsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedInputs);

        // Act
        await _worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        
        _mockDevice.Raise(d => d.InputsChanged += null, expectedInputs);
        await Task.Delay(200); // Wait for async processing
        
        _cts.Cancel();

        // Assert
        var actualInputs = await _channel.Reader.ReadAsync();
        Assert.Equal(expectedInputs, actualInputs);
    }

    [Fact]
    public async Task ReadInputs_LogsWarning_WhenChannelIsFull()
    {
        // Arrange
        var inputs = new List<bool> { true }.AsReadOnly();
        
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockDevice.Setup(d => d.ReadInputsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(inputs);

        // Fill the channel to capacity
        for (int i = 0; i < 10; i++)
        {
            await _channel.Writer.WriteAsync(inputs);
        }

        // Act
        await _worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        
        _mockDevice.Raise(d => d.InputsChanged += null, inputs);
        await Task.Delay(200); // Wait for processing
        
        _cts.Cancel();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Dropped input snapshot")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReadInputs_LogsError_WhenExceptionOccurs()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Device error");
        
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockDevice.Setup(d => d.ReadInputsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        
        _mockDevice.Raise(d => d.InputsChanged += null, new List<bool>().AsReadOnly());
        await Task.Delay(200); // Wait for error handling
        
        _cts.Cancel();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error reading inputs")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StopsGracefully_WhenCancelled()
    {
        // Arrange
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        
        _cts.Cancel();
        await _worker.StopAsync(CancellationToken.None);

        // Assert - should complete without throwing
        Assert.True(_cts.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task OnInputsChanged_HandlesMultipleEvents_Concurrently()
    {
        // Arrange
        var inputs = new List<bool> { true }.AsReadOnly();
        var callCount = 0;
        
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockDevice.Setup(d => d.ReadInputsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(inputs)
            .Callback(() => Interlocked.Increment(ref callCount));

        // Act
        await _worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        
        // Trigger multiple events
        _mockDevice.Raise(d => d.InputsChanged += null, inputs);
        _mockDevice.Raise(d => d.InputsChanged += null, inputs);
        _mockDevice.Raise(d => d.InputsChanged += null, inputs);
        
        await Task.Delay(500); // Wait for all to process
        _cts.Cancel();

        // Assert
        Assert.True(callCount >= 3, $"Expected at least 3 calls, got {callCount}");
    }

    [Fact]
    public async Task Constructor_InitializesAllDependencies()
    {
        // Arrange & Act
        var worker = new InputMonitoringWorker(
            _mockDevice.Object,
            _channel,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(worker);
    }

    [Fact]
    public async Task ReadInputs_DoesNotThrow_WhenChannelWriteFails()
    {
        // Arrange
        var inputs = new List<bool> { true }.AsReadOnly();
        
        _mockDevice.Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockDevice.Setup(d => d.ReadInputsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(inputs);

        // Fill channel
        for (int i = 0; i < 10; i++)
        {
            await _channel.Writer.WriteAsync(inputs);
        }

        // Act & Assert - should not throw
        await _worker.StartAsync(_cts.Token);
        await Task.Delay(100);
        
        _mockDevice.Raise(d => d.InputsChanged += null, inputs);
        await Task.Delay(200);
        
        _cts.Cancel();
        
        // If we get here without exception, test passes
        Assert.True(true);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _channel?.Writer.Complete();
    }
}