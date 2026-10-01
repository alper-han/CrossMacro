namespace CrossMacro.Core.Tests.Models;

public sealed class ShortcutTaskMultipleTriggerTests
{
    [Fact]
    public void TrySetEnabled_AcceptsTaskWithAtLeastOneHotkey()
    {
        var task = new ShortcutTask
        {
            MacroFilePath = "macro.macro",
        };
        task.Hotkeys.Add("Space");

        _ = task.TrySetEnabled(enabled: true).Should().BeTrue();
        _ = task.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TrySetEnabled_RejectsTaskWithoutHotkeys()
    {
        var task = new ShortcutTask
        {
            MacroFilePath = "macro.macro",
        };

        _ = task.TrySetEnabled(enabled: true).Should().BeFalse();
        _ = task.IsEnabled.Should().BeFalse();
    }
}
