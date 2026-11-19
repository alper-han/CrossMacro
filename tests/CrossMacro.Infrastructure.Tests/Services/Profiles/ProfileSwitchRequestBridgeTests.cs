namespace CrossMacro.Infrastructure.Tests.Services.Profiles;

public sealed class ProfileSwitchRequestBridgeTests
{
    [Fact]
    public async Task RequestSwitchAsync_AwaitsResolvedHandler()
    {
        var handler = Substitute.For<IProfileSwitchRequestHandler>();
        var bridge = new ProfileSwitchRequestBridge(() => handler);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = handler.HandleSwitchRequestAsync("work").Returns(completion.Task);

        var request = bridge.RequestSwitchAsync("work");

        _ = request.IsCompleted.Should().BeFalse();
        completion.SetResult();
        await request;
        await handler.Received(1).HandleSwitchRequestAsync("work");
    }

    [Fact]
    public async Task RequestSwitchAsync_ResolvesTheHandlerOnlyWhenARequestArrives()
    {
        var handler = Substitute.For<IProfileSwitchRequestHandler>();
        _ = handler.HandleSwitchRequestAsync("work").Returns(Task.CompletedTask);
        var resolutions = 0;
        var bridge = new ProfileSwitchRequestBridge(() =>
        {
            resolutions++;
            return handler;
        });

        Assert.Equal(0, resolutions);
        await bridge.RequestSwitchAsync("work");

        Assert.Equal(1, resolutions);
    }
}
