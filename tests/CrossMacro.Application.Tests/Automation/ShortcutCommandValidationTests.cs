namespace CrossMacro.Application.Tests.Automation;

public sealed class ShortcutCommandValidationTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task AddAsync_RejectsIncompatiblePressReleaseModeBeforePersistence(bool held, bool loop)
    {
        var workflow = Substitute.For<IManageShortcut>();
        workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>([], scopeGeneration: 0));
        var commands = ShortcutCommandTestFactory.Create(workflow);
        var result = await commands.ExecuteAsync(new ShortcutCommand(ShortcutCommandAction.Add,
            Name: "Pair", MacroFilePath: "press.macro", Hotkeys: ["F7"],
            RunOnPressAndRelease: true, ReleaseMacroFilePath: "release.macro", RunWhileHeld: held, Loop: loop), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal("Invalid shortcut mode.", result.Message);
        await workflow.DidNotReceive().AddAsync(Arg.Any<ShortcutTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EditAsync_RejectsReleasePathWithoutPairModeAndLeavesOriginalUntouched()
    {
        var original = new ShortcutTask { Name = "Original", MacroFilePath = "press.macro", Hotkeys = { "F7" } };
        var workflow = Substitute.For<IManageShortcut>();
        workflow.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCollectionResult<ShortcutTask>([original], scopeGeneration: 0));
        var commands = ShortcutCommandTestFactory.Create(workflow);
        var result = await commands.ExecuteAsync(new ShortcutCommand(ShortcutCommandAction.Edit,
            TaskId: original.Id.ToString(), Name: "Changed", ReleaseMacroFilePath: "release.macro"), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal("Original", original.Name);
        Assert.Empty(original.ReleaseMacroFilePath);
        await workflow.DidNotReceive().UpdateAsync(Arg.Any<ShortcutTask>(), Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

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
