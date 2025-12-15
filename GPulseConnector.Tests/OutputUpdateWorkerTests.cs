using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using GPulseConnector.Abstraction.Interfaces;
using GPulseConnector.Abstraction.Models;
using GPulseConnector.Workers;

namespace GPulseConnector.Tests.Workers
{
    public class OutputUpdateWorkerTests
    {
        [Fact]
        public async Task UpdateOutputAsync_ValidMapping_StartsBlinkerAndSetsOutputs()
        {
            // Arrange
            var deviceMock = new Mock<IOutputDevice>();
            var cacheMock = new Mock<IPatternMappingCache>();
            var blinkerMock = new Mock<IBlinker>();
            var loggerMock = new Mock<ILogger<OutputUpdateWorker>>();

            Func<IReadOnlyList<bool>, Task>? callback = null;

            blinkerMock
                .Setup(b => b.StartOrRestartAsync(
                    It.IsAny<IReadOnlyList<bool>>(),
                    It.IsAny<Func<IReadOnlyList<bool>, Task>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyList<bool>, Func<IReadOnlyList<bool>, Task>, int, int, CancellationToken>(
                    (_, cb, _, _, _) => callback = cb)
                .Returns(Task.CompletedTask);

            var mapping = new PatternMapping
            {
                OD0 = true,
                OD1 = false,
                OD2 = true
            };

            var worker = new OutputUpdateWorker(
                deviceMock.Object,
                cacheMock.Object,
                blinkerMock.Object,
                loggerMock.Object);

            // Act
            await worker.UpdateOutputAsync(mapping, CancellationToken.None);

            await callback!(new[] { true, false, true });

            // Assert
            blinkerMock.Verify(
                b => b.StartOrRestartAsync(
                    It.IsAny<IReadOnlyList<bool>>(),
                    It.IsAny<Func<IReadOnlyList<bool>, Task>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            deviceMock.Verify(
                d => d.SetOutputsAsync(
                    It.IsAny<IReadOnlyList<bool>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
