using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Brainboxes.IO;

namespace GPulseConnector.Abstraction.Interfaces
{
    public interface IInputDevice
    {
        // Raised when input states change
        event Action<IReadOnlyList<bool>>? InputsChanged;

        // Raised when the device connection status changes (true = connected, false = disconnected)
        event Action<bool>? DeviceDisconnected;

        // Connects to the input device
        Task ConnectAsync(CancellationToken token = default);

        // Starts monitoring input and connection status
        Task StartMonitoringAsync(CancellationToken token = default);

        // Reads the current input values
        Task<IReadOnlyList<bool>> ReadInputsAsync(CancellationToken token = default);
    }
}
