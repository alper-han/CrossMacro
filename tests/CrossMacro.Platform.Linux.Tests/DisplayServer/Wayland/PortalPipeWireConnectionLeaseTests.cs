namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalPipeWireConnectionLeaseTests
{
    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_AndReleasesOnce()
    {
        var releaseCalls = 0;
        using var lease = CreateLease(() => releaseCalls++);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, releaseCalls);
    }

    private static PortalPipeWireConnectionLease CreateLease(Action release) =>
        new(
            CreateConnection(),
            release);

    // The real constructor needs libpipewire; the lease only stores the connection and never touches it.
    private static PortalPipeWireConnection CreateConnection() =>
        (PortalPipeWireConnection)RuntimeHelpers.GetUninitializedObject(typeof(PortalPipeWireConnection));
}
