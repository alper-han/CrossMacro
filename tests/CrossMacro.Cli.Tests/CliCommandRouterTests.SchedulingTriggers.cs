// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Cli.Tests;

public sealed partial class CliCommandRouterTests
{

    [Fact]
    public void Parse_WhenScheduleListWithJson_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["schedule", "list", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScheduleListCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScheduleRunWithJson_ReturnsOptions()
    {
        const string id = "11111111-1111-1111-1111-111111111111";
        var result = CliCommandRouterAccessor.Parse(["schedule", "run", id, "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScheduleRunCliOptions>(result.Options);
        Assert.Equal(id, options.TaskId);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScheduleRunMissingTaskIdAndJsonProvided_ReturnsUsageError()
    {
        var result = CliCommandRouterAccessor.Parse(["schedule", "run", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("schedule run requires <task-id>.", result.ErrorMessage);
        Assert.Equal(["Usage: crossmacro schedule run <task-id> [--json] [--log-level <level>]"], result.ErrorDetails);
    }

    [Fact]
    public void Parse_WhenScheduleAddInterval_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["schedule", "add", "--name", "Daily", "--macro", "/tmp/demo.macro", "--interval", "10m", "--enabled", "true", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScheduleCliOptions>(result.Options);
        Assert.Equal(ScheduleCliAction.Add, options.Action);
        Assert.Equal("Daily", options.Name);
        Assert.Equal("/tmp/demo.macro", options.MacroFilePath);
        Assert.Equal("10m", options.Interval);
        Assert.True(options.Enabled);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScheduleEditWeekly_ReturnsOptions()
    {
        const string id = "11111111-1111-1111-1111-111111111111";
        var result = CliCommandRouterAccessor.Parse(["schedule", "edit", id, "--weekly", "mon,wed", "--time", "09:30"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScheduleCliOptions>(result.Options);
        Assert.Equal(ScheduleCliAction.Edit, options.Action);
        Assert.Equal(id, options.TaskId);
        Assert.Equal("mon,wed", options.Weekly);
        Assert.Equal("09:30", options.Time);
    }

    [Fact]
    public void Parse_WhenScheduleNext_ReturnsOptions()
    {
        const string id = "11111111-1111-1111-1111-111111111111";
        var result = CliCommandRouterAccessor.Parse(["schedule", "next", id, "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScheduleCliOptions>(result.Options);
        Assert.Equal(ScheduleCliAction.Next, options.Action);
        Assert.Equal(id, options.TaskId);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScheduleAddUsesMultipleScheduleForms_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["schedule", "add", "--name", "Bad", "--macro", "/tmp/demo.macro", "--interval", "10m", "--at", "2026-07-05T18:00:00"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Use only one schedule form", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenScheduleEditHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["schedule", "edit", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("schedule.edit", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenShortcutListWithJson_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["shortcut", "list", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ShortcutListCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenShortcutRunWithJson_ReturnsOptions()
    {
        const string id = "22222222-2222-2222-2222-222222222222";
        var result = CliCommandRouterAccessor.Parse(["shortcut", "run", id, "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ShortcutRunCliOptions>(result.Options);
        Assert.Equal(id, options.TaskId);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenShortcutRunMissingTaskIdAndJsonProvided_ReturnsUsageError()
    {
        var result = CliCommandRouterAccessor.Parse(["shortcut", "run", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("shortcut run requires <task-id>.", result.ErrorMessage);
        Assert.Equal(["Usage: crossmacro shortcut run <task-id> [--json] [--log-level <level>]"], result.ErrorDetails);
    }

    [Fact]
    public void Parse_WhenShortcutAdd_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["shortcut", "add", "--name", "Demo", "--macro", "/tmp/demo.macro", "--hotkey", "Ctrl+Alt+D", "--repeat", "3", "--repeat-delay-ms", "250", "--enabled", "true", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ShortcutCliOptions>(result.Options);
        Assert.Equal(ShortcutCliAction.Add, options.Action);
        Assert.Equal("Demo", options.Name);
        Assert.Equal("/tmp/demo.macro", options.MacroFilePath);
        Assert.Equal("Ctrl+Alt+D", options.Hotkey);
        Assert.Equal(3, options.RepeatCount);
        Assert.Equal(250, options.RepeatDelayMs);
        Assert.True(options.Enabled);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenShortcutEditRandomDelay_ReturnsOptions()
    {
        const string id = "22222222-2222-2222-2222-222222222222";
        var result = CliCommandRouterAccessor.Parse(["shortcut", "edit", id, "--random-repeat-delay", "100", "200", "--run-while-held"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ShortcutCliOptions>(result.Options);
        Assert.Equal(ShortcutCliAction.Edit, options.Action);
        Assert.Equal(id, options.TaskId);
        Assert.Equal(100, options.RepeatDelayMinMs);
        Assert.Equal(200, options.RepeatDelayMaxMs);
        Assert.True(options.RunWhileHeld);
    }

    [Fact]
    public void Parse_WhenShortcutBind_ReturnsOptions()
    {
        const string id = "22222222-2222-2222-2222-222222222222";
        var result = CliCommandRouterAccessor.Parse(["shortcut", "bind", id, "Ctrl+Shift+M", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ShortcutCliOptions>(result.Options);
        Assert.Equal(ShortcutCliAction.Bind, options.Action);
        Assert.Equal(id, options.TaskId);
        Assert.Equal("Ctrl+Shift+M", options.Hotkey);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenShortcutAddMissingRequiredOptions_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["shortcut", "add", "--name", "Demo", "--macro", "/tmp/demo.macro"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("shortcut add requires", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenShortcutBindHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["shortcut", "bind", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("shortcut.bind", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenTriggerListWithJson_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["trigger", "list", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<TriggerListCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenTriggerAdd_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "trigger", "add",
            "--name", "Firefox Dev",
            "--field", "WindowTitle",
            "--match-mode", "Regex",
            "--value", ".*Firefox.*",
            "--action", "SwitchProfile",
            "--profile", "dev",
            "--fire-mode", "OnceOnChange",
            "--cooldown-ms", "1000",
            "--debounce-ms", "250",
            "--enabled", "true",
            "--json",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<TriggerCliOptions>(result.Options);
        Assert.Equal(TriggerCliAction.Add, options.Action);
        Assert.Equal("Firefox Dev", options.Name);
        Assert.Equal(TriggerField.WindowTitle, options.Field);
        Assert.Equal(TriggerMatchMode.Regex, options.MatchMode);
        Assert.Equal(".*Firefox.*", options.Value);
        Assert.Equal(TriggerOperation.SwitchProfile, options.TriggerActionVal);
        Assert.Equal("dev", options.TargetProfileId);
        Assert.Equal(TriggerFireMode.OnceOnChange, options.FireMode);
        Assert.Equal(1000, options.CooldownMs);
        Assert.Equal(250, options.DebounceMs);
        Assert.True(options.Enabled);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenTriggerEdit_ReturnsOptions()
    {
        const string id = "33333333-3333-3333-3333-333333333333";
        var result = CliCommandRouterAccessor.Parse(["trigger", "edit", id, "--debounce-ms", "500", "--cooldown-ms", "1500"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<TriggerCliOptions>(result.Options);
        Assert.Equal(TriggerCliAction.Edit, options.Action);
        Assert.Equal(id, options.TaskId);
        Assert.Equal(500, options.DebounceMs);
        Assert.Equal(1500, options.CooldownMs);
    }

    [Fact]
    public void Parse_WhenTriggerAddMissingRequiredOptions_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["trigger", "add", "--name", "Firefox Dev", "--field", "WindowTitle"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("trigger add requires", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenShortcutRunHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["shortcut", "run", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("shortcut.run", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenScheduleRunHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["schedule", "run", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("schedule.run", result.HelpTopic);
    }
}
