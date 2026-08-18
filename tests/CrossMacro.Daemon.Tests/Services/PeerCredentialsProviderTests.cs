namespace CrossMacro.Daemon.Tests.Services;


public sealed class PeerCredentialsProviderTests
{
    [Fact]
    public void IsUserInGroup_DelegatesToNssResolver()
    {
        var resolver = new FakeGroupMembershipResolver { Result = true };
        var provider = new PeerCredentialsProvider(resolver);

        var result = provider.IsUserInGroup(1000, "crossmacro");

        Assert.True(result);
        Assert.Equal((1000u, "crossmacro"), resolver.Request);
    }

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

    private sealed class FakeGroupMembershipResolver : IUserGroupMembershipResolver
    {
        public bool Result { get; init; }
        public (uint Uid, string GroupName)? Request { get; private set; }

        public bool IsUserInGroup(uint uid, string groupName)
        {
            Request = (uid, groupName);
            return Result;
        }
    }
}
