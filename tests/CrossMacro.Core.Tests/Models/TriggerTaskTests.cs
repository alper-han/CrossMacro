
namespace CrossMacro.Core.Tests.Models;

public class TriggerTaskTests
{
    [Fact]
    public void CanBeEnabled_RequiresValueAndTargetProfileForSwitchProfile()
    {
        var t = new TriggerTask { Value = "firefox", Action = TriggerOperation.SwitchProfile };
        t.CanBeEnabled.Should().BeFalse();

        t.TargetProfileId = "gaming";
        t.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void CanBeEnabled_ForNonSwitchProfile_IgnoresTargetProfile()
    {
        // Unknown action values default to bypassing the profile validation rules.
        var t = new TriggerTask { Value = "firefox", Action = (TriggerOperation)999 };
        t.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void CanBeEnabled_RequiresMacroFilePathForRunMacro()
    {
        var t = new TriggerTask { Value = "firefox", Action = TriggerOperation.RunMacro };
        t.CanBeEnabled.Should().BeFalse("RunMacro requires a macro file path");

        t.MacroFilePath = "/tmp/demo.macro";
        t.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_RefusesEnableWhenCanBeEnabledIsFalse()
    {
        var t = new TriggerTask { Value = "", Action = TriggerOperation.SwitchProfile };
        t.TrySetEnabled(true).Should().BeFalse();
        t.IsEnabled.Should().BeFalse("gate should block enabling incomplete task");
    }

    [Fact]
    public void IsEnabled_AcceptsEnableWhenCanBeEnabledIsTrue()
    {
        var t = new TriggerTask
        {
            Value = "firefox",
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "gaming",
        };
        t.TrySetEnabled(true).Should().BeTrue();
        t.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TriggerTask_IsPlainDomainModel()
    {
        typeof(TriggerTask).GetInterface(nameof(System.ComponentModel.INotifyPropertyChanged))
            .Should().BeNull();
    }

    [Fact]
    public void CanBeEnabled_FieldNone_DoesNotRequireValue()
    {
        // TriggerField.None is the pure-interval path: no Value concept needed.
        var t = new TriggerTask
        {
            Field = TriggerField.None,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "gaming",
        };
        t.CanBeEnabled.Should().BeTrue("None field fires regardless of window state, no Value required");
    }

    [Fact]
    public void CooldownMs_And_DebounceMs_DefaultToNull()
    {
        var t = new TriggerTask();
        t.CooldownMs.Should().BeNull();
        t.DebounceMs.Should().BeNull();

        t.CooldownMs = 500;
        t.DebounceMs = 50;
        t.CooldownMs.Should().Be(500);
        t.DebounceMs.Should().Be(50);
    }
}
