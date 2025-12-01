using GPulseConnector.Abstraction.Models;

namespace GPulseConnector.Abstraction.Interfaces
{
    public interface IOutputDevice
    {
        int OutputCount { get; }

        Task ConnectAsync(CancellationToken cancellationToken = default);

        Task DisconnectAsync(CancellationToken cancellationToken = default);

        Task SetOutputAsync(int outputIndex, bool value, CancellationToken cancellationToken = default);

        Task SetOutputsAsync(IReadOnlyList<bool> outputs, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<bool>> ReadOutputsAsync(CancellationToken cancellationToken = default);

        event Action<int, bool>? OutputChanged;
        event Action<bool>? DeviceDisconnected;

    }
}
