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

    [Fact]
    public void Constructor_RejectsNullRelease()
    {
        Assert.Throws<ArgumentNullException>(() => new PortalPipeWireConnectionLease(CreateConnection(), null!));
    }

    [Fact]
    public void Constructor_RejectsNullConnection()
    {
        Assert.Throws<ArgumentNullException>(() => new PortalPipeWireConnectionLease(null!, static () => { }));
    }

    private static PortalPipeWireConnectionLease CreateLease(Action release) =>
        new(
            CreateConnection(),
            release);

    private static PortalPipeWireConnection CreateConnection() =>
        (PortalPipeWireConnection)RuntimeHelpers.GetUninitializedObject(typeof(PortalPipeWireConnection));
}
