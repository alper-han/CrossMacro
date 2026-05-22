using CrossMacro.Platform.MacOS.Services;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class MacOSMousePositionProviderTests
{
    [Fact]
    public void ReadPosition_WhenEventRefIsZero_ReturnsNull()
    {
        var position = MacOSMousePositionProvider.ReadPosition(IntPtr.Zero);

        Assert.Null(position);
    }
}
