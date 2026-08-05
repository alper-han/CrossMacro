// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Cli.Tests;

public sealed partial class CliCommandRouterTests
{

    [Fact]
    public void Parse_WhenSettingsGetWithKey_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "get", "playback.speed", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsGetCliOptions>(result.Options);
        Assert.Equal("playback.speed", options.Key);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenSettingsSet_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "set", "playback.loop", "true", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsSetCliOptions>(result.Options);
        Assert.Equal("playback.loop", options.Key);
        Assert.Equal("true", options.Value);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenSettingsSetWithNegativeNumericValue_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "set", "playback.speed", "-0.5", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsSetCliOptions>(result.Options);
        Assert.Equal("playback.speed", options.Key);
        Assert.Equal("-0.5", options.Value);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenSettingsSetMissingValueAndJsonProvided_ReturnsUsageError()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "set", "logging.level", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("settings set requires <key> and <value>.", result.ErrorMessage);
        Assert.Equal(["Usage: crossmacro settings set <key> <value> [--json] [--log-level <level>]"], result.ErrorDetails);
    }

    [Fact]
    public void Parse_WhenSettingsGetAll_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "get", "--all", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsGetCliOptions>(result.Options);
        Assert.True(options.All);
        Assert.Null(options.Key);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenSettingsListKeys_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "list-keys", "--json"]);

        Assert.True(result.IsSuccess);
        _ = Assert.IsType<SettingsListKeysCliOptions>(result.Options);
    }

    [Fact]
    public void Parse_WhenSettingsReset_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "reset", "ui.theme", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsResetCliOptions>(result.Options);
        Assert.Equal("ui.theme", options.Key);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenProfileDeleteWithForce_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["profile", "delete", "work", "--force", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ProfileCliOptions>(result.Options);
        Assert.Equal(ProfileCliAction.Delete, options.Action);
        Assert.Equal("work", options.ProfileIdentifier);
        Assert.True(options.Force);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenProfileRename_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["profile", "rename", "work", "office"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ProfileCliOptions>(result.Options);
        Assert.Equal(ProfileCliAction.Rename, options.Action);
        Assert.Equal("work", options.ProfileIdentifier);
        Assert.Equal("office", options.NewName);
    }

    [Fact]
    public void Parse_WhenTextExpansionAddWithOptions_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "text-expansion",
            "add",
            ":mail",
            "me@example.com",
            "--method",
            "CtrlShiftV",
            "--insertion-mode",
            "DirectTyping",
            "--direct-typing-method",
            "CompatibleKeyByKey",
            "--profile",
            "work",
            "--json",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<TextExpansionCliOptions>(result.Options);
        Assert.Equal(TextExpansionCliAction.Add, options.Action);
        Assert.Equal(":mail", options.Trigger);
        Assert.Equal("me@example.com", options.Replacement);
        Assert.Equal(CrossMacro.Core.Models.PasteMethod.CtrlShiftV, options.Method);
        Assert.Equal(CrossMacro.Core.Models.TextInsertionMode.DirectTyping, options.InsertionMode);
        Assert.Equal(CrossMacro.Core.Models.DirectTypingMethod.CompatibleKeyByKey, options.DirectTypingMethod);
        Assert.Equal("work", options.ProfileIdentifier);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenSettingsGetHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "get", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("settings.get", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenSettingsRootHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("settings", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenSettingsSetHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["settings", "set", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("settings.set", result.HelpTopic);
    }

    [Fact]
    public void GetUsage_WhenSettingsTopic_ContainsSupportedKeys()
    {
        var usage = CliCommandRouterAccessor.GetUsage("settings");

        Assert.Contains("Supported Keys:", usage, StringComparison.Ordinal);
        Assert.Contains("playback.speed", usage, StringComparison.Ordinal);
        Assert.Contains("logging.level", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void GetUsage_WhenSettingsSetTopic_ContainsValueNotes()
    {
        var usage = CliCommandRouterAccessor.GetUsage("settings.set");

        Assert.Contains("Value Notes:", usage, StringComparison.Ordinal);
        Assert.Contains("Debug|Information|Warning|Error", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenClipboardGetWithJson_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["clipboard", "get", "--json", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ClipboardCliOptions>(result.Options);
        Assert.Equal(ClipboardCliAction.Get, options.Action);
        Assert.True(options.JsonOutput);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenClipboardSetFile_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["clipboard", "set", "--file", "/tmp/message.txt", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ClipboardCliOptions>(result.Options);
        Assert.Equal(ClipboardCliAction.Set, options.Action);
        Assert.Equal("/tmp/message.txt", options.FilePath);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenClipboardClearWithJson_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["clipboard", "clear", "--json", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ClipboardCliOptions>(result.Options);
        Assert.Equal(ClipboardCliAction.Clear, options.Action);
        Assert.True(options.JsonOutput);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenClipboardClearHasOperand_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["clipboard", "clear", "extra"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("clipboard clear", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenClipboardSetHasTextAndFile_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["clipboard", "set", "hello", "--file", "/tmp/message.txt"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("either <text> or --file", result.ErrorMessage, StringComparison.Ordinal);
    }
}
