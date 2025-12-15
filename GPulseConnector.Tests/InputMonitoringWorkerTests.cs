using GPulseConnector.Abstraction.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;


public class InputMonitoringWorkerTests
{
[Fact]
public async Task InputsChanged_WritesToChannel()
{
    // Arrange
    var cts = new CancellationTokenSource();

    var channel = Channel.CreateUnbounded<IReadOnlyList<bool>>();

    var deviceMock = new Mock<IInputDevice>();
    var loggerMock = new Mock<ILogger<InputMonitoringWorker>>();

    deviceMock
        .Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var worker = new InputMonitoringWorker(
        deviceMock.Object,
        channel,
        loggerMock.Object);

    // Act
    await worker.StartAsync(cts.Token);

    var inputSnapshot = new List<bool> { true, false, true };

    // Raise the event safely
    deviceMock.Raise(
        d => d.InputsChanged += null!,
        inputSnapshot);

    var read = await channel.Reader.ReadAsync();

    cts.Cancel();
    await worker.StopAsync(CancellationToken.None);

    // Assert
    Assert.Equal(inputSnapshot, read);
}


    [Fact]
    public async Task StopAsync_CompletesChannel()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<IReadOnlyList<bool>>();

        var deviceMock = new Mock<IInputDevice>();
        var loggerMock = new Mock<ILogger<InputMonitoringWorker>>();

        deviceMock
            .Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new InputMonitoringWorker(
            deviceMock.Object,
            channel,
            loggerMock.Object);

        var cts = new CancellationTokenSource();

        // Act
        await worker.StartAsync(cts.Token);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(channel.Reader.Completion.IsCompleted);
    }

    [Fact]
public async Task InputsChanged_ChannelFull_DropsEvent()
{
    // Arrange
    var channel = Channel.CreateBounded<IReadOnlyList<bool>>(1);

    var deviceMock = new Mock<IInputDevice>();
    var loggerMock = new Mock<ILogger<InputMonitoringWorker>>();

    deviceMock
        .Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var worker = new InputMonitoringWorker(
        deviceMock.Object,
        channel,
        loggerMock.Object);

    var cts = new CancellationTokenSource();

    // Act
    await worker.StartAsync(cts.Token);

    deviceMock.Raise(d => d.InputsChanged += null!, new[] { true });
    deviceMock.Raise(d => d.InputsChanged += null!, new[] { false });

    cts.Cancel();
    await worker.StopAsync(CancellationToken.None);

    // Assert
    Assert.True(channel.Reader.TryRead(out _));
    Assert.False(channel.Reader.TryRead(out _));
}

[Fact]
public async Task InputsChanged_ChannelFull_LogsWarning()
{
    // Arrange
    var channel = Channel.CreateBounded<IReadOnlyList<bool>>(1);

    var deviceMock = new Mock<IInputDevice>();
    var loggerMock = new Mock<ILogger<InputMonitoringWorker>>();

    deviceMock
        .Setup(d => d.StartMonitoringAsync(It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var worker = new InputMonitoringWorker(
        deviceMock.Object,
        channel,
        loggerMock.Object);

    var cts = new CancellationTokenSource();

    // Act
    await worker.StartAsync(cts.Token);

    deviceMock.Raise(d => d.InputsChanged += null!, new[] { true });
    deviceMock.Raise(d => d.InputsChanged += null!, new[] { false });

    cts.Cancel();
    await worker.StopAsync(CancellationToken.None);

    // Assert
    loggerMock.Verify(
        l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) =>
                v.ToString()!.Contains("Dropped input snapshot")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
        Times.Once);
}


}
