
namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class LinuxIpcProviderSupportTests
{
    [Fact]
    public async Task LinuxIpcInputSimulator_AfterDispose_RejectsInitializationAndSimulation()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        using var simulator = new LinuxIpcInputSimulator(client);
        simulator.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => simulator.Initialize());
        _ = Assert.Throws<ObjectDisposedException>(() => simulator.MoveRelative(1, 2));
        _ = Assert.Throws<ObjectDisposedException>(() => simulator.SimulateBatch([]));
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => simulator.SimulateBatchAsync([], TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => simulator.SimulateAbsoluteTrajectoryAsync([], TestContext.Current.CancellationToken));
    }
}
