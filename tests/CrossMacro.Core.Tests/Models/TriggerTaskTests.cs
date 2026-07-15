using System;
using CrossMacro.Core.Models;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Core.Tests.Models;

public class TriggerTaskTests
{
    [Fact]
    public void CanBeEnabled_RequiresValueAndTargetProfileForSwitchProfile()
    {
        var t = new TriggerTask { Value = "firefox", Action = TriggerAction.SwitchProfile };
        t.CanBeEnabled.Should().BeFalse();

        t.TargetProfileId = "gaming";
        t.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void CanBeEnabled_ForNonSwitchProfile_IgnoresTargetProfile()
    {
        // Unknown action values default to bypassing the profile validation rules.
        var t = new TriggerTask { Value = "firefox", Action = (TriggerAction)999 };
        t.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void CanBeEnabled_RequiresMacroFilePathForRunMacro()
    {
        var t = new TriggerTask { Value = "firefox", Action = TriggerAction.RunMacro };
        t.CanBeEnabled.Should().BeFalse("RunMacro requires a macro file path");

        t.MacroFilePath = "/tmp/demo.macro";
        t.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_RefusesEnableWhenCanBeEnabledIsFalse()
    {
        var t = new TriggerTask { Value = "", Action = TriggerAction.SwitchProfile };
        t.IsEnabled = true;
        t.IsEnabled.Should().BeFalse("gate should block enabling incomplete task");
    }

    [Fact]
    public void IsEnabled_AcceptsEnableWhenCanBeEnabledIsTrue()
    {
        var t = new TriggerTask
        {
            Value = "firefox",
            Action = TriggerAction.SwitchProfile,
            TargetProfileId = "gaming",
        };
        t.IsEnabled = true;
        t.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SettingValue_RaisesPropertyChangedAndCanBeEnabled()
    {
        var t = new TriggerTask { Action = TriggerAction.SwitchProfile, TargetProfileId = "gaming" };
        var changed = new System.Collections.Generic.List<string?>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        t.Value = "firefox";

        changed.Should().Contain(nameof(TriggerTask.Value));
        changed.Should().Contain(nameof(TriggerTask.CanBeEnabled));
        t.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void CanBeEnabled_FieldNone_DoesNotRequireValue()
    {
        // TriggerField.None is the pure-interval path: no Value concept needed.
        var t = new TriggerTask
        {
            Field = TriggerField.None,
            Action = TriggerAction.SwitchProfile,
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
