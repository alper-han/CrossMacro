using System.IO;
using CrossMacro.Cli;

namespace CrossMacro.Cli.Tests;

public class CliCommandRouterTests
{
    private readonly CliCommandRouter _router = new();

    [Fact]
    public void Parse_WhenNoArgs_StartsGui()
    {
        var result = _router.Parse([]);

        Assert.True(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        Assert.False(result.ShowHelp);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_WhenVersionToken_ReturnsVersion()
    {
        var result = _router.Parse(["--version"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShowVersion);
        Assert.False(result.ShouldStartGui);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_WhenMacroValidateWithJson_ReturnsOptions()
    {
        var result = _router.Parse(["macro", "validate", "/tmp/test.macro", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        var options = Assert.IsType<MacroValidateCliOptions>(result.Options);
        Assert.Equal("/tmp/test.macro", options.MacroFilePath);
        Assert.True(options.JsonOutput);
        Assert.Null(options.LogLevel);
    }

    [Fact]
    public void Parse_WhenMacroInfoWithJson_ReturnsOptions()
    {
        var result = _router.Parse(["macro", "info", "/tmp/test.macro", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        var options = Assert.IsType<MacroInfoCliOptions>(result.Options);
        Assert.Equal("/tmp/test.macro", options.MacroFilePath);
        Assert.True(options.JsonOutput);
        Assert.Null(options.LogLevel);
    }

    [Fact]
    public void Parse_WhenMacroHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["macro", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("macro", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenMacroValidateHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["macro", "validate", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("macro.validate", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenUnknownOption_ReturnsError()
    {
        var result = _router.Parse(["macro", "validate", "/tmp/test.macro", "--bad"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown option", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenUnknownTokenWithoutCliPrefix_ReturnsUnknownCommandError()
    {
        var result = _router.Parse(["some-random-token"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown command", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenKnownGuiDashedToken_StartsGui()
    {
        var result = _router.Parse(["--drm"]);

        Assert.True(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parse_WhenUnknownDashedToken_ReturnsUnknownOptionError()
    {
        var result = _router.Parse(["--unknown-switch"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown option", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenKnownGuiDisplaySwitchWithValue_StartsGui()
    {
        var result = _router.Parse(["--display=:0"]);

        Assert.True(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parse_WhenExistingPathToken_ReturnsUnknownCommandError()
    {
        var path = Path.GetTempFileName();
        try
        {
            var result = _router.Parse([path]);

            Assert.False(result.ShouldStartGui);
            Assert.False(result.IsSuccess);
            Assert.Contains("Unknown command", result.ErrorMessage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_WhenStandaloneJsonFlagWithoutCommand_ReturnsError()
    {
        var result = _router.Parse(["--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("requires a command", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WhenStandaloneLogLevelFlagWithoutCommand_ReturnsError()
    {
        var result = _router.Parse(["--log-level", "debug"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("requires a command", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WhenPlayWithOptions_ReturnsPlayOptions()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--speed", "1.5", "--loop", "--repeat", "3", "--repeat-delay-ms", "200", "--countdown", "1", "--timeout", "30", "--dry-run", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.Equal("/tmp/test.macro", options.MacroFilePath);
        Assert.Equal(1.5, options.SpeedMultiplier);
        Assert.True(options.Loop);
        Assert.Equal(3, options.RepeatCount);
        Assert.Equal(200, options.RepeatDelayMs);
        Assert.Equal(1, options.CountdownSeconds);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.True(options.DryRun);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenPlayWithDetach_ReturnsError()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--detach"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown option for play", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenPlayWithLogLevel_ReturnsNormalizedLevel()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenPlayRepeatWithoutLoop_EnablesLoopSemantics()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--repeat", "50"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.True(options.Loop);
        Assert.Equal(50, options.RepeatCount);
    }

    [Fact]
    public void Parse_WhenPlayLoopWithoutRepeat_UsesInfiniteLoopDefaults()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--loop"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.True(options.Loop);
        Assert.Equal(0, options.RepeatCount);
    }

    [Fact]
    public void Parse_WhenPlayRepeatZeroWithoutLoop_ReturnsError()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--repeat", "0"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("requires --loop", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WhenPlayRepeatZeroWithLoop_IsAllowed()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--loop", "--repeat", "0"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.True(options.Loop);
        Assert.Equal(0, options.RepeatCount);
    }

    [Fact]
    public void Parse_WhenPlayHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["play", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("play", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenPlayHasInvalidRepeat_ReturnsError()
    {
        var result = _router.Parse(["play", "/tmp/test.macro", "--repeat", "-2"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--repeat must be >= 0", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenDoctorWithVerboseAndJson_ReturnsDoctorOptions()
    {
        var result = _router.Parse(["doctor", "--verbose", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<DoctorCliOptions>(result.Options);
        Assert.True(options.Verbose);
        Assert.True(options.JsonOutput);
        Assert.Null(options.LogLevel);
    }

    [Fact]
    public void Parse_WhenDoctorWithInvalidLogLevel_ReturnsError()
    {
        var result = _router.Parse(["doctor", "--log-level", "trace"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid value for --log-level", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRecordWithAllOptions_ReturnsRecordOptions()
    {
        var result = _router.Parse([
            "record",
            "--output", "/tmp/out.macro",
            "--mouse", "true",
            "--keyboard", "false",
            "--mode", "absolute",
            "--skip-initial-zero",
            "--duration", "10",
            "--json",
            "--log-level", "warning"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RecordCliOptions>(result.Options);
        Assert.Equal("/tmp/out.macro", options.OutputFilePath);
        Assert.True(options.RecordMouse);
        Assert.False(options.RecordKeyboard);
        Assert.Equal(RecordCoordinateMode.Absolute, options.CoordinateMode);
        Assert.True(options.SkipInitialZero);
        Assert.Equal(10, options.DurationSeconds);
        Assert.True(options.JsonOutput);
        Assert.Equal("Warning", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenRecordMissingOutput_ReturnsError()
    {
        var result = _router.Parse(["record", "--mode", "auto"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Usage: record --output", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRecordModeInvalid_ReturnsError()
    {
        var result = _router.Parse(["record", "--output", "/tmp/a.macro", "--mode", "invalid"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid value for --mode", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRunWithSteps_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "--step", "move abs 100 100",
            "--step", "click left",
            "--speed", "1.5",
            "--countdown", "2",
            "--timeout", "30",
            "--dry-run",
            "--json"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(2, options.Steps.Count);
        Assert.Equal("move abs 100 100", options.Steps[0]);
        Assert.Equal("click left", options.Steps[1]);
        Assert.Equal(1.5, options.SpeedMultiplier);
        Assert.Equal(2, options.CountdownSeconds);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.True(options.DryRun);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenRunWithFileOnly_ReturnsRunOptions()
    {
        var result = _router.Parse(["run", "--file", "/tmp/steps.txt", "--dry-run"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("/tmp/steps.txt", options.StepFilePath);
        Assert.Empty(options.Steps);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_WhenRunWithInlineSteps_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "move", "abs", "100", "200",
            "click", "left",
            "delay", "40",
            "type", "hello"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(4, options.Steps.Count);
        Assert.Equal("move abs 100 200", options.Steps[0]);
        Assert.Equal("click left", options.Steps[1]);
        Assert.Equal("delay 40", options.Steps[2]);
        Assert.Equal("type hello", options.Steps[3]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineCurrentPositionClick_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "move", "abs", "100", "200",
            "click", "current", "left"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(2, options.Steps.Count);
        Assert.Equal("move abs 100 200", options.Steps[0]);
        Assert.Equal("click current left", options.Steps[1]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineRepeatAndSet_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "set", "n", "2",
            "repeat", "$n", "{",
            "click", "left",
            "delay", "random", "10", "20",
            "}"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(5, options.Steps.Count);
        Assert.Equal("set n 2", options.Steps[0]);
        Assert.Equal("repeat $n {", options.Steps[1]);
        Assert.Equal("click left", options.Steps[2]);
        Assert.Equal("delay random 10 20", options.Steps[3]);
        Assert.Equal("}", options.Steps[4]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineIfWhileFor_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "if", "$x", "==", "1", "{",
            "click", "left",
            "}",
            "while", "$i", "<", "3", "{",
            "inc", "i",
            "}",
            "for", "n", "from", "1", "to", "5", "step", "2", "{",
            "click", "right",
            "}"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("if $x == 1 {", options.Steps[0]);
        Assert.Equal("while $i < 3 {", options.Steps[3]);
        Assert.Equal("inc i", options.Steps[4]);
        Assert.Equal("for n from 1 to 5 step 2 {", options.Steps[6]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineBreakAndContinue_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "repeat", "2", "{",
            "continue",
            "}",
            "for", "i", "from", "1", "to", "2", "{",
            "break",
            "}"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(6, options.Steps.Count);
        Assert.Equal("repeat 2 {", options.Steps[0]);
        Assert.Equal("continue", options.Steps[1]);
        Assert.Equal("}", options.Steps[2]);
        Assert.Equal("for i from 1 to 2 {", options.Steps[3]);
        Assert.Equal("break", options.Steps[4]);
        Assert.Equal("}", options.Steps[5]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineIncVariableAmount_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "set", "step", "2",
            "inc", "i", "$step",
            "click", "left"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("set step 2", options.Steps[0]);
        Assert.Equal("inc i $step", options.Steps[1]);
        Assert.Equal("click left", options.Steps[2]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineRepeatMissingBrace_ReturnsError()
    {
        var result = _router.Parse([
            "run",
            "repeat", "3",
            "click", "left"
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid inline step syntax for repeat", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRunWithInvalidInlineMove_ReturnsError()
    {
        var result = _router.Parse(["run", "move", "abs", "100"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid inline step syntax for move", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRunWithoutStep_ReturnsError()
    {
        var result = _router.Parse(["run", "--dry-run"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Usage: run --step", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRunSpeedBelowRange_ReturnsError()
    {
        var result = _router.Parse(["run", "--step", "click left", "--speed", "0.09"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--speed must be between 0.1 and 10.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRunSpeedAboveRange_ReturnsError()
    {
        var result = _router.Parse(["run", "--step", "click left", "--speed", "10.01"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--speed must be between 0.1 and 10.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenRunHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["run", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("run", result.HelpTopic);
    }

    [Fact]
    public void GetUsage_WhenRunTopic_ContainsPhase2Details()
    {
        var usage = _router.GetUsage("run");

        Assert.Contains("--file <steps-file>", usage);
        Assert.Contains("type <text>", usage);
        Assert.Contains("break | continue", usage);
        Assert.Contains("Examples:", usage);
    }

    [Fact]
    public void Parse_WhenHeadlessCommand_ReturnsHeadlessOptions()
    {
        var result = _router.Parse(["headless", "--json", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<HeadlessCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenHeadlessFlag_ReturnsHeadlessOptions()
    {
        var result = _router.Parse(["--headless"]);

        Assert.True(result.IsSuccess);
        Assert.IsType<HeadlessCliOptions>(result.Options);
    }

    [Fact]
    public void Parse_WhenSettingsGetWithKey_ReturnsOptions()
    {
        var result = _router.Parse(["settings", "get", "playback.speed", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsGetCliOptions>(result.Options);
        Assert.Equal("playback.speed", options.Key);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenSettingsSet_ReturnsOptions()
    {
        var result = _router.Parse(["settings", "set", "playback.loop", "true", "--json"]);

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
        var result = _router.Parse(["settings", "set", "playback.speed", "-0.5", "--json"]);

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
        var result = _router.Parse(["settings", "set", "logging.level", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Usage: settings set <key> <value> [--json] [--log-level <level>]", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenScheduleListWithJson_ReturnsOptions()
    {
        var result = _router.Parse(["schedule", "list", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScheduleListCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScheduleRunWithJson_ReturnsOptions()
    {
        const string id = "11111111-1111-1111-1111-111111111111";
        var result = _router.Parse(["schedule", "run", id, "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScheduleRunCliOptions>(result.Options);
        Assert.Equal(id, options.TaskId);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScheduleRunMissingTaskIdAndJsonProvided_ReturnsUsageError()
    {
        var result = _router.Parse(["schedule", "run", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Usage: schedule run <task-id> [--json] [--log-level <level>]", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenShortcutListWithJson_ReturnsOptions()
    {
        var result = _router.Parse(["shortcut", "list", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ShortcutListCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenShortcutRunWithJson_ReturnsOptions()
    {
        const string id = "22222222-2222-2222-2222-222222222222";
        var result = _router.Parse(["shortcut", "run", id, "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ShortcutRunCliOptions>(result.Options);
        Assert.Equal(id, options.TaskId);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenShortcutRunMissingTaskIdAndJsonProvided_ReturnsUsageError()
    {
        var result = _router.Parse(["shortcut", "run", "--json"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Usage: shortcut run <task-id> [--json] [--log-level <level>]", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenSettingsGetHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["settings", "get", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("settings.get", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenSettingsRootHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["settings", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("settings", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenSettingsSetHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["settings", "set", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("settings.set", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenShortcutRunHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["shortcut", "run", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("shortcut.run", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenScheduleRunHelp_ReturnsHelpWithTopic()
    {
        var result = _router.Parse(["schedule", "run", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("schedule.run", result.HelpTopic);
    }

    [Fact]
    public void GetUsage_WhenSettingsTopic_ContainsSupportedKeys()
    {
        var usage = _router.GetUsage("settings");

        Assert.Contains("Supported Keys:", usage);
        Assert.Contains("playback.speed", usage);
        Assert.Contains("logging.level", usage);
    }

    [Fact]
    public void GetUsage_WhenSettingsSetTopic_ContainsValueNotes()
    {
        var usage = _router.GetUsage("settings.set");

        Assert.Contains("Value Notes:", usage);
        Assert.Contains("Debug|Information|Warning|Error", usage);
    }

    [Fact]
    public void GetUsage_WhenDefault_ContainsRecordShortOptionAndHeadlessAlias()
    {
        var usage = _router.GetUsage();

        Assert.Contains("record (--output|-o)", usage);
        Assert.Contains("crossmacro --headless", usage);
    }
}
