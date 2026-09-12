namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ProfileSwitchRequestBridgeTests
{
    [Fact]
    public async Task RequestSwitchAsync_AwaitsConfiguredHandler()
    {
        var bridge = new ProfileSwitchRequestBridge();
        var handler = Substitute.For<IProfileSwitchRequestHandler>();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = handler.HandleSwitchRequestAsync("work").Returns(completion.Task);
        bridge.SetHandler(handler);

        var request = bridge.RequestSwitchAsync("work");

        _ = request.IsCompleted.Should().BeFalse();
        completion.SetResult();
        await request;
        await handler.Received(1).HandleSwitchRequestAsync("work");
    }

    [Fact]
    public async Task RequestSwitchAsync_WithoutHandler_ThrowsDeterministically()
    {
        var bridge = new ProfileSwitchRequestBridge();

        Func<Task> act = () => bridge.RequestSwitchAsync("work");

        _ = (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*No profile switch request handler*");
    }

    [Fact]
    public void SetHandler_WhenHandlerIsNull_ThrowsArgumentNullException()
    {
        var bridge = new ProfileSwitchRequestBridge();

        _ = Assert.Throws<ArgumentNullException>(() => bridge.SetHandler(null!));
    }

    [Fact]
    public async Task SetHandler_WhenCalledTwice_RejectsReplacementAndKeepsFirstHandler()
    {
        var bridge = new ProfileSwitchRequestBridge();
        var first = Substitute.For<IProfileSwitchRequestHandler>();
        var second = Substitute.For<IProfileSwitchRequestHandler>();
        _ = first.HandleSwitchRequestAsync("work").Returns(Task.CompletedTask);
        bridge.SetHandler(first);

        _ = Assert.Throws<InvalidOperationException>(() => bridge.SetHandler(second));

        await bridge.RequestSwitchAsync("work");
        await first.Received(1).HandleSwitchRequestAsync("work");
        await second.DidNotReceive().HandleSwitchRequestAsync(Arg.Any<string>());
    }
}
