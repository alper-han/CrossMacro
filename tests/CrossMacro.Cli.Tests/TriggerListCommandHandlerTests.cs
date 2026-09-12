namespace CrossMacro.Cli.Tests;

public sealed class TriggerListCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new TriggerListCommandHandler(triggerCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesCancellationAndResult()
    {
        var service = Substitute.For<ITriggerCliService>();
        _ = service.ListAsync(Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Loaded 1 trigger task."));
        var handler = new TriggerListCommandHandler(service);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await handler.ExecuteAsync(new TriggerListCliOptions(JsonOutput: true), cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Loaded 1 trigger task.", result.Message);
        _ = await service.Received(1).ListAsync(cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_PropagatesFailure()
    {
        var service = Substitute.For<ITriggerCliService>();
        _ = service.ListAsync(Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Triggers unavailable.",
                errors: ["Trigger store could not be loaded."]));
        var handler = new TriggerListCommandHandler(service);

        var result = await handler.ExecuteAsync(new TriggerListCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Equal("Triggers unavailable.", result.Message);
        Assert.Equal(["Trigger store could not be loaded."], result.Errors);
    }
}
