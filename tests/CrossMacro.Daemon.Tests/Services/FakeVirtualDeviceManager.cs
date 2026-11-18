using CrossMacro.Daemon.Services.Devices;

namespace CrossMacro.Daemon.Tests.Services;

internal sealed class FakeVirtualDeviceManager : IVirtualDeviceManager
{
    public Exception? ThrowOnInitialConfigure { get; set; }
    public Exception? ThrowOnReconfigure { get; set; }
    public Exception? ThrowOnSendEvent { get; set; }
    public Exception? ThrowOnSendEvents { get; set; }
    public List<(int Width, int Height)> ConfigureCalls { get; } = [];
    public List<(ushort Type, ushort Code, int Value)> SentEvents { get; } = [];

    public Task ConfigureAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ConfigureCalls.Add((width, height));

        if (width is 0 && height is 0 && ThrowOnInitialConfigure is not null)
        {
            throw ThrowOnInitialConfigure;
        }

        if ((width is not 0 || height is not 0) && ThrowOnReconfigure is not null)
        {
            throw ThrowOnReconfigure;
        }

        return Task.CompletedTask;
    }

    public Task SendEventAsync(ushort type, ushort code, int value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SentEvents.Add((type, code, value));

        if (ThrowOnSendEvent is not null)
        {
            throw ThrowOnSendEvent;
        }

        return Task.CompletedTask;
    }

    public Task SendEventsAsync(IReadOnlyList<IpcSimulationRequest> events, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var inputEvent in events)
        {
            SentEvents.Add((inputEvent.Type, inputEvent.Code, inputEvent.Value));
        }

        if (ThrowOnSendEvents is not null)
        {
            throw ThrowOnSendEvents;
        }

        return Task.CompletedTask;
    }

    public int ReleasePressedKeysCalls { get; private set; }

    public Task ReleasePressedKeysAsync(CancellationToken cancellationToken = default)
    {
        ReleasePressedKeysCalls++;
        return Task.CompletedTask;
    }
}
