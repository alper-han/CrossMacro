
namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSMousePositionProviderTests
{
    [Fact]
    public void ReadPosition_WhenEventRefIsZero_ReturnsNull()
    {
        var position = MacOSMousePositionProvider.ReadPosition(IntPtr.Zero);

        Assert.Null(position);
    }
}
