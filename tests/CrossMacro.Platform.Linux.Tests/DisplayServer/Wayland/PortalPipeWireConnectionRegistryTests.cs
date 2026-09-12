namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalPipeWireConnectionRegistryTests
{
    [Fact]
    public void Acquire_RejectsNullRemote()
    {
        Assert.Throws<ArgumentNullException>(() => PortalPipeWireConnectionRegistry.Acquire(null!));
    }

    [Fact]
    public void Acquire_RejectsClosedRemoteBeforeNativeConnectionCreation()
    {
        using var remote = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
        remote.Dispose();

        var exception = Assert.Throws<ArgumentException>(() => PortalPipeWireConnectionRegistry.Acquire(remote));

        Assert.Equal("remote", exception.ParamName);
    }
}
