namespace CrossMacro.Cli.Tests;

public sealed class TriggerCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new TriggerCommandHandler(triggerCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesOptionsAndCancellation()
    {
        var service = Substitute.For<ITriggerCliService>();
        var options = new TriggerCliOptions(
            TriggerCliAction.Add,
            TaskId: "33333333-3333-3333-3333-333333333333",
            Name: "Window title",
            Field: TriggerField.WindowTitle,
            MatchMode: TriggerMatchMode.Contains,
            Value: "Editor",
            TriggerActionVal: TriggerOperation.RunMacro,
            TargetProfileId: "work",
            MacroFilePath: "macro.json",
            FireMode: TriggerFireMode.OnEnter,
            CooldownMs: 100,
            DebounceMs: 50,
            Enabled: true);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        _ = service.ExecuteAsync(options, cancellationToken)
            .Returns(CliCommandExecutionResult.Ok("Trigger added."));

        var handler = new TriggerCommandHandler(service);
        var result = await handler.ExecuteAsync(options, cancellationToken);

        Assert.True(result.Success);
        Assert.Equal("Trigger added.", result.Message);
        _ = await service.Received(1).ExecuteAsync(options, cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenServiceFails_PropagatesFailure()
    {
        var service = Substitute.For<ITriggerCliService>();
        var options = new TriggerCliOptions(TriggerCliAction.Remove, TaskId: "33333333-3333-3333-3333-333333333333");
        _ = service.ExecuteAsync(options, Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Trigger task not found.",
                errors: ["No trigger task found."]));

        var handler = new TriggerCommandHandler(service);
        var result = await handler.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        Assert.Equal("Trigger task not found.", result.Message);
        Assert.Equal(["No trigger task found."], result.Errors);
        _ = await service.Received(1).ExecuteAsync(options, CancellationToken.None);
    }
}
