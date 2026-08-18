namespace CrossMacro.Core.Tests.Models;

public sealed class ShortcutTaskTests
{
    [Fact]
    public void ShortcutTask_IsPlainDomainModel()
    {
        _ = typeof(ShortcutTask).GetInterface(nameof(INotifyPropertyChanged)).Should().BeNull();
    }

    [Fact]
    public void TrySetEnabled_RejectsIncompleteTask()
    {
        var task = new ShortcutTask();

        _ = task.TrySetEnabled(enabled: true).Should().BeFalse();
        _ = task.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Normalize_EnforcesLoopExclusivityAndDelayRange()
    {
        var task = new ShortcutTask
        {
            LoopEnabled = true,
            RunWhileHeld = true,
            RepeatDelayMinMs = 500,
            RepeatDelayMaxMs = 10,
        };

        task.Normalize();

        _ = task.LoopEnabled.Should().BeTrue();
        _ = task.RunWhileHeld.Should().BeFalse();
        _ = task.RepeatDelayMinMs.Should().Be(500);
        _ = task.RepeatDelayMaxMs.Should().Be(500);
    }

    [Fact]
    public void TrySetEnabled_RejectsInvalidWindowRules()
    {
        var task = new ShortcutTask
        {
            MacroFilePath = "macro.macro",
            HotkeyString = "F8",
        };
        task.WindowRules.Add(new ShortcutWindowRule
        {
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Regex,
            Value = "[",
        });

        _ = task.TrySetEnabled(enabled: true).Should().BeFalse();
        _ = task.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CanBeEnabled_RejectsInvalidWindowRuleEntries()
    {
        var task = new ShortcutTask
        {
            MacroFilePath = "macro.macro",
            HotkeyString = "F8",
        };
        task.WindowRules.Add(new ShortcutWindowRule
        {
            Field = TriggerField.Workspace,
            MatchMode = TriggerMatchMode.Equals,
            Value = "1",
        });

        _ = task.CanBeEnabled.Should().BeFalse();
        _ = task.TrySetEnabled(enabled: true).Should().BeFalse();
    }

}
