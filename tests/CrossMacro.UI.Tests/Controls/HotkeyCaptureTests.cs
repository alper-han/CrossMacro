using CrossMacro.UI.Controls;

namespace CrossMacro.UI.Tests.Controls;

public sealed class HotkeyCaptureTests
{
    [Fact]
    public void ImplementsDisposableOwnershipContract()
    {
        Assert.Contains(typeof(IDisposable), typeof(HotkeyCapture).GetInterfaces());
    }
}
