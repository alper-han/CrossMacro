using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.TestInfrastructure;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public class LinuxIpcProviderSupportTests
{
    [LinuxFact]
    public void LinuxIpcInputSimulator_IsSupported_WhenProbeFails_ReturnsFalse()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        using var simulator = new LinuxIpcInputSimulator(client, () => false);

        Assert.False(simulator.IsSupported);
    }

    [LinuxFact]
    public void LinuxIpcInputSimulator_IsSupported_WhenProbePasses_ReturnsTrue()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        using var simulator = new LinuxIpcInputSimulator(client, () => true);

        Assert.True(simulator.IsSupported);
    }

    [LinuxFact]
    public void LinuxIpcInputCapture_IsSupported_WhenProbeFails_ReturnsFalse()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        using var capture = new LinuxIpcInputCapture(client, isSupportedProbe: () => false);

        Assert.False(capture.IsSupported);
    }

    [LinuxFact]
    public void LinuxIpcInputCapture_IsSupported_WhenProbePasses_ReturnsTrue()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);
        using var capture = new LinuxIpcInputCapture(client, isSupportedProbe: () => true);

        Assert.True(capture.IsSupported);
    }
}
