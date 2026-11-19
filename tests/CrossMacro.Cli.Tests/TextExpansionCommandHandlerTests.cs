using CrossMacro.Commands.Automation;
using CrossMacro.Core.Models.Automation.TextExpansion;

namespace CrossMacro.Cli.Tests;

public sealed class TextExpansionCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_List_DelegatesProfileAndCancellation()
    {
        var service = Substitute.For<ITextExpansionCommandService>();
        _ = service.ListAsync("work", Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Loaded expansions."));
        var handler = new TextExpansionCommandHandler(service);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await handler.ExecuteAsync(
            new TextExpansionOptions(TextExpansionAction.List, ProfileIdentifier: "work"),
            cancellationToken);

        Assert.True(result.Success);
        _ = await service.Received(1).ListAsync("work", cancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_Add_DelegatesAllOptions()
    {
        var service = Substitute.For<ITextExpansionCommandService>();
        _ = service.AddAsync(
                "brb",
                "be right back",
                PasteMethod.CtrlShiftV,
                TextInsertionMode.DirectTyping,
                DirectTypingMethod.CompatibleKeyByKey,
                "work",
                Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Expansion added."));
        var handler = new TextExpansionCommandHandler(service);
        var options = new TextExpansionOptions(
            TextExpansionAction.Add,
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
        var service = Substitute.For<ITextExpansionCommandService>();
        var handler = new TextExpansionCommandHandler(service);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await handler.ExecuteAsync(
            new TextExpansionOptions((TextExpansionAction)999),
            cancellationToken);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.InvalidArguments, result.Outcome);
        _ = service.DidNotReceiveWithAnyArgs().ListAsync(default, cancellationToken);
    }
}
