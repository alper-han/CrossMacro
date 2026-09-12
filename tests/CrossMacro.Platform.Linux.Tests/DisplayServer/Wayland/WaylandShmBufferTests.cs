namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class WaylandShmBufferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RejectsNonPositiveSizeBeforeNativeAllocation(int size)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => WaylandShmBuffer.Create(size));
    }
}
