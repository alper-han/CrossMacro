namespace CrossMacro.Cli.Tests;

public sealed class TextExpansionCommandHandlerTests
{
    [Fact]
    public void Constructor_WhenServiceIsNull_Throws()
    {
#pragma warning disable CS8625 // Intentionally pass null to exercise the constructor guard.
        var act = () => new TextExpansionCommandHandler(textExpansionCliService: null);
#pragma warning restore CS8625

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_List_DelegatesProfileAndCancellation()
    {
        var service = Substitute.For<ITextExpansionCliService>();
        _ = service.ListAsync("work", Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Loaded expansions."));
        var handler = new TextExpansionCommandHandler(service);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await handler.ExecuteAsync(
            new TextExpansionCliOptions(TextExpansionCliAction.List, ProfileIdentifier: "work"),
            cancellationToken);

        Assert.True(result.Success);
        _ = await service.Received(1).ListAsync("work", cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_Add_DelegatesAllOptions()
    {
        var service = Substitute.For<ITextExpansionCliService>();
        _ = service.AddAsync(
                "brb",
                "be right back",
                PasteMethod.CtrlShiftV,
                TextInsertionMode.DirectTyping,
                DirectTypingMethod.CompatibleKeyByKey,
                "work",
                Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Expansion added."));
        var handler = new TextExpansionCommandHandler(service);
        var options = new TextExpansionCliOptions(
            TextExpansionCliAction.Add,
            Trigger: "brb",
            Replacement: "be right back",
            Method: PasteMethod.CtrlShiftV,
            InsertionMode: TextInsertionMode.DirectTyping,
            DirectTypingMethod: DirectTypingMethod.CompatibleKeyByKey,
            ProfileIdentifier: "work");

        var result = await handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        _ = await service.Received(1).AddAsync(
            "brb",
            "be right back",
            PasteMethod.CtrlShiftV,
            TextInsertionMode.DirectTyping,
            DirectTypingMethod.CompatibleKeyByKey,
            "work",
            CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAction_ReturnsInvalidArgumentsWithoutCallingService()
    {
        var service = Substitute.For<ITextExpansionCliService>();
        var handler = new TextExpansionCommandHandler(service);

#pragma warning disable CS0618 // Deliberately use an undefined enum value for the boundary test.
        var result = await handler.ExecuteAsync(
            new TextExpansionCliOptions((TextExpansionCliAction)999),
            CancellationToken.None);
#pragma warning restore CS0618

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        _ = service.DidNotReceiveWithAnyArgs().ListAsync(default, default);
    }
}
