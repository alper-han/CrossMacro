namespace CrossMacro.Application.Tests.Automation;

public sealed class ShortcutCommandValidationTests
{
    [Fact]
    public async Task AddAsync_RejectsBlankHotkeyEntryInsteadOfSkippingIt()
    {
        var workflow = Substitute.For<IManageShortcut>();
        _ = workflow.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new TaskCollectionResult<ShortcutTask>([], scopeGeneration: 0));
        var commands = ShortcutCommandTestFactory.Create(workflow);

        var result = await commands.ExecuteAsync(
            new ShortcutCommand(
                ShortcutCommandAction.Add,
                Name: "Demo",
                MacroFilePath: "demo.macro",
                Hotkeys: ["Ctrl+A", " "]),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Invalid shortcut hotkeys.", result.Message);
        Assert.Equal("A shortcut chord is required.", Assert.Single(result.Errors));
        await workflow.DidNotReceive().AddAsync(
            Arg.Any<ShortcutTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }
}
