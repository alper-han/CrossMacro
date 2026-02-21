namespace CrossMacro.Daemon.Tests.Services;

using CrossMacro.Daemon.Services;

public class PeerCredentialsProviderTests
{
    [Fact]
    public void WrapperMethods_ShouldNotThrowForInvalidInputs()
    {
        var provider = new PeerCredentialsProvider();

        var ex = Record.Exception(() =>
        {
            _ = provider.GetProcessExecutable(-1);
            _ = provider.IsUserInGroup(uint.MaxValue, "crossmacro");
        });

        Assert.Null(ex);
    }
}
