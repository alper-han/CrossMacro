using CrossMacro.Daemon.Security;

namespace CrossMacro.Daemon.Tests.Security;

public class PolkitCheckerTests
{
    [Fact]
    public void Actions_ShouldUseCanonicalIoGithubNamespace()
    {
        Assert.StartsWith("io.github.alper_han.crossmacro.", PolkitChecker.Actions.InputCapture);
        Assert.StartsWith("io.github.alper_han.crossmacro.", PolkitChecker.Actions.InputSimulate);
        Assert.Equal("io.github.alper_han.crossmacro.input-capture", PolkitChecker.Actions.InputCapture);
        Assert.Equal("io.github.alper_han.crossmacro.input-simulate", PolkitChecker.Actions.InputSimulate);
    }
}
