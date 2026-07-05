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
    public void Parse_WhenStartMinimizedGuiFlag_StartsGui()
    {
        var result = _router.Parse(["--start-minimized"]);

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
        Assert.True(result.PrefersJsonOutput);
        Assert.Equal("Option --json requires a command.", result.ErrorMessage);
        Assert.Contains("See crossmacro --help for usage information.", result.ErrorDetails);
    }

    [Fact]
    public void Parse_WhenCommandParseFailsAfterJsonFlag_PrefersJsonOutput()
    {
        var result = _router.Parse(["doctor", "--bad", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.True(result.PrefersJsonOutput);
        Assert.Contains("Unknown option for doctor", result.ErrorMessage, StringComparison.Ordinal);
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
        Assert.Equal("record requires --output <macro-file>.", result.ErrorMessage);
        Assert.Contains("Usage: crossmacro record (--output|-o) <macro-file>", result.ErrorDetails[0]);
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
    public void Parse_WhenRunWithInlineScreenReadingSteps_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "pixelcolor", "1", "2", "sampled",
            "waitcolor", "3", "4", "00FF00", "100", "wait_ok",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "found", "found_x", "found_y", "tolerance", "26",
            "click", "left"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("pixelcolor 1 2 sampled", options.Steps[0]);
        Assert.Equal("waitcolor 3 4 00FF00 100 wait_ok", options.Steps[1]);
        Assert.Equal("pixelsearch 0 0 10 10 FF0000 found found_x found_y tolerance 26", options.Steps[2]);
        Assert.Equal("click left", options.Steps[3]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineScreenReadingOptionalForms_ReturnsRunOptions()
    {
        var result = _router.Parse([
            "run",
            "pixelcolor", "rel", "-1", "2",
            "pixelcolor", "rel", "-3", "4", "relativeSampled",
            "waitcolor", "3", "4", "00FF00",
            "pixelsearch", "0", "0", "10", "10", "FF0000", "tolerance", "26",
            "pixelsearch", "1", "2", "11", "12", "00FF00", "found_x", "found_y", "tolerance", "7",
            "click", "left"
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("pixelcolor rel -1 2", options.Steps[0]);
        Assert.Equal("pixelcolor rel -3 4 relativeSampled", options.Steps[1]);
        Assert.Equal("waitcolor 3 4 00FF00", options.Steps[2]);
        Assert.Equal("pixelsearch 0 0 10 10 FF0000 tolerance 26", options.Steps[3]);
        Assert.Equal("pixelsearch 1 2 11 12 00FF00 found_x found_y tolerance 7", options.Steps[4]);
        Assert.Equal("click left", options.Steps[5]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineMalformedScreenReadingPrefix_ReturnsError()
    {
        var result = _router.Parse(["run", "pixelcolorful", "1", "2", "sampled"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown inline run step command: pixelcolorful", result.ErrorMessage);
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
        Assert.Equal("run requires at least one --step argument or --file.", result.ErrorMessage);
        Assert.Equal(2, result.ErrorDetails.Count);
        Assert.Contains("Usage: crossmacro run --step <step>", result.ErrorDetails[0]);
        Assert.Contains("Usage: crossmacro run <step-command>", result.ErrorDetails[1]);
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
    public void GetUsage_WhenRunHelp_IncludesScreenReadingSteps()
    {
        var usage = _router.GetUsage("run");

        Assert.Contains("pixelcolor <x> <y> [var]", usage);
        Assert.Contains("pixelcolor rel <dx> <dy> [var]", usage);
        Assert.Contains("waitcolor <x> <y> <RRGGBB|$var> [timeout_ms] [result_var]", usage);
        Assert.Contains("pixelsearch <x1> <y1> <x2> <y2> <RRGGBB|$var> [found_var var_x var_y|var_x var_y] [tolerance <0..255>]", usage);
    }

    [Fact]
    public void GetUsage_WhenRunHelp_IncludesShellStep()
    {
        var usage = _router.GetUsage("run");

        Assert.Contains("shell \"<command>\" [retries] [backoff_ms] [timeout_ms]", usage);
        Assert.Contains("shell capture \"<command>\" exit_var stdout_var stderr_var", usage);
        Assert.Contains("shell input \"<stdin text>\" \"<command>\"", usage);
        Assert.Contains("shell capture-input \"<stdin text>\" \"<command>\" exit_var stdout_var stderr_var", usage);
        Assert.Contains("Capture modes do not fail on non-zero exits", usage);
        Assert.Contains("capped at 65536 characters", usage);
        Assert.Contains("only run trusted macros", usage);
        Assert.Contains("Flatpak builds disable shell steps", usage);
        Assert.Contains("Use $$NAME to pass $NAME to the shell", usage);
    }

    [Fact]
    public void Parse_WhenInlineShellStepHasOptions_ReturnsRunOptions()
    {
        var result = _router.Parse(["run", "shell", "printf ok", "1", "250", "5000"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell \"printf ok\" 1 250 5000"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellStepContainsQuotes_EscapesCommandPayload()
    {
        var result = _router.Parse(["run", "shell", "printf \"ok\""]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell \"printf \\\"ok\\\"\""], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCaptureStepHasVariables_ReturnsRunOptions()
    {
        var result = _router.Parse(["run", "shell", "capture", "printf ok", "code", "out", "err", "1", "250", "5000"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell capture \"printf ok\" code out err 1 250 5000"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellInputStepHasPayload_ReturnsRunOptions()
    {
        var result = _router.Parse(["run", "shell", "input", "hello", "cat", "0", "0", "1000"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell input \"hello\" \"cat\" 0 0 1000"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCaptureInputStepHasPayload_ReturnsRunOptions()
    {
        var result = _router.Parse(["run", "shell", "capture-input", "hello", "cat", "code", "out", "err"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell capture-input \"hello\" \"cat\" code out err"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCommandContainsBackslashes_EscapesBackslashes()
    {
        var result = _router.Parse(["run", "shell", @"printf C:\temp\"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal([@"shell ""printf C:\\temp\\"""], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCaptureInputContainsBackslashes_EscapesBackslashes()
    {
        var result = _router.Parse(["run", "shell", "capture-input", @"C:\temp\", "cat", "code", "out", "err"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal([@"shell capture-input ""C:\\temp\\"" ""cat"" code out err"], options.Steps);
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
        Assert.Equal("settings set requires <key> and <value>.", result.ErrorMessage);
        Assert.Equal(["Usage: crossmacro settings set <key> <value> [--json] [--log-level <level>]"], result.ErrorDetails);
    }

    [Fact]
    public void Parse_WhenSettingsGetAll_ReturnsOptions()
    {
        var result = _router.Parse(["settings", "get", "--all", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsGetCliOptions>(result.Options);
        Assert.True(options.All);
        Assert.Null(options.Key);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenSettingsListKeys_ReturnsOptions()
    {
        var result = _router.Parse(["settings", "list-keys", "--json"]);

        Assert.True(result.IsSuccess);
        Assert.IsType<SettingsListKeysCliOptions>(result.Options);
    }

    [Fact]
    public void Parse_WhenSettingsReset_ReturnsOptions()
    {
        var result = _router.Parse(["settings", "reset", "ui.theme", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<SettingsResetCliOptions>(result.Options);
        Assert.Equal("ui.theme", options.Key);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenProfileDeleteWithForce_ReturnsOptions()
    {
        var result = _router.Parse(["profile", "delete", "work", "--force", "--json"]);

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
        var result = _router.Parse(["profile", "rename", "work", "office"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ProfileCliOptions>(result.Options);
        Assert.Equal(ProfileCliAction.Rename, options.Action);
        Assert.Equal("work", options.ProfileIdentifier);
        Assert.Equal("office", options.NewName);
    }

    [Fact]
    public void Parse_WhenTextExpansionAddWithOptions_ReturnsOptions()
    {
        var result = _router.Parse([
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
            "--json"
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
        Assert.Equal("schedule run requires <task-id>.", result.ErrorMessage);
        Assert.Equal(["Usage: crossmacro schedule run <task-id> [--json] [--log-level <level>]"], result.ErrorDetails);
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
        Assert.Equal("shortcut run requires <task-id>.", result.ErrorMessage);
        Assert.Equal(["Usage: crossmacro shortcut run <task-id> [--json] [--log-level <level>]"], result.ErrorDetails);
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

        Assert.Contains("crossmacro [--start-minimized]", usage);
        Assert.Contains("record (--output|-o)", usage);
        Assert.Contains("crossmacro clipboard get", usage);
        Assert.Contains("crossmacro clipboard clear", usage);
        Assert.Contains("crossmacro window active|list", usage);
        Assert.Contains("crossmacro screen pixel|wait-color|search-color", usage);
        Assert.Contains("crossmacro screenshot", usage);
        Assert.Contains("crossmacro profile", usage);
        Assert.Contains("crossmacro text-expansion", usage);
        Assert.Contains("crossmacro --headless", usage);
    }

    [Fact]
    public void Parse_WhenClipboardGetWithJson_ReturnsOptions()
    {
        var result = _router.Parse(["clipboard", "get", "--json", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ClipboardCliOptions>(result.Options);
        Assert.Equal(ClipboardCliAction.Get, options.Action);
        Assert.True(options.JsonOutput);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenClipboardSetFile_ReturnsOptions()
    {
        var result = _router.Parse(["clipboard", "set", "--file", "/tmp/message.txt", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ClipboardCliOptions>(result.Options);
        Assert.Equal(ClipboardCliAction.Set, options.Action);
        Assert.Equal("/tmp/message.txt", options.FilePath);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenClipboardClearWithJson_ReturnsOptions()
    {
        var result = _router.Parse(["clipboard", "clear", "--json", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ClipboardCliOptions>(result.Options);
        Assert.Equal(ClipboardCliAction.Clear, options.Action);
        Assert.True(options.JsonOutput);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenClipboardClearHasOperand_ReturnsError()
    {
        var result = _router.Parse(["clipboard", "clear", "extra"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("clipboard clear", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenClipboardSetHasTextAndFile_ReturnsError()
    {
        var result = _router.Parse(["clipboard", "set", "hello", "--file", "/tmp/message.txt"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("either <text> or --file", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenWindowCommands_ReturnTypedOptions()
    {
        var search = _router.Parse(["window", "search", "--title", "Firefox", "--json"]);
        var wait = _router.Parse(["window", "wait", "--class", "Code", "--timeout-ms", "1500"]);
        var move = _router.Parse(["window", "move", "--active", "10", "20"]);
        var workspace = _router.Parse(["window", "workspace", "move-window", "--address", "0xabc", "dev"]);

        Assert.True(search.IsSuccess);
        Assert.Equal(WindowCliAction.Search, Assert.IsType<WindowCliOptions>(search.Options).Action);
        Assert.True(Assert.IsType<WindowCliOptions>(search.Options).JsonOutput);
        Assert.True(wait.IsSuccess);
        Assert.Equal(1500, Assert.IsType<WindowCliOptions>(wait.Options).TimeoutMs);
        Assert.True(move.IsSuccess);
        Assert.Equal(10, Assert.IsType<WindowCliOptions>(move.Options).X);
        Assert.True(workspace.IsSuccess);
        var workspaceOptions = Assert.IsType<WindowCliOptions>(workspace.Options);
        Assert.Equal(WindowCliAction.WorkspaceMoveWindow, workspaceOptions.Action);
        Assert.Equal("dev", workspaceOptions.WorkspaceName);
    }

    [Fact]
    public void Parse_WhenWindowFocusHasMultipleSelectors_ReturnsError()
    {
        var result = _router.Parse(["window", "focus", "--title", "A", "--class", "B"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only one window selector", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenScreenCommands_ReturnTypedOptions()
    {
        var pixel = _router.Parse(["screen", "pixel", "--relative", "-1", "2", "--json"]);
        var wait = _router.Parse(["screen", "wait-color", "3", "4", "00ff00", "--timeout-ms", "500"]);
        var search = _router.Parse(["screen", "search-color", "0", "0", "10", "20", "FF0000", "--tolerance", "26"]);

        Assert.True(pixel.IsSuccess);
        var pixelOptions = Assert.IsType<ScreenCliOptions>(pixel.Options);
        Assert.True(pixelOptions.Relative);
        Assert.Equal(-1, pixelOptions.X);
        Assert.True(pixelOptions.JsonOutput);
        Assert.True(wait.IsSuccess);
        Assert.Equal(500, Assert.IsType<ScreenCliOptions>(wait.Options).TimeoutMs);
        Assert.True(search.IsSuccess);
        Assert.Equal(26, Assert.IsType<ScreenCliOptions>(search.Options).Tolerance);
    }

    [Fact]
    public void Parse_WhenScreenSearchToleranceOutOfRange_ReturnsError()
    {
        var result = _router.Parse(["screen", "search-color", "0", "0", "10", "20", "FF0000", "--tolerance", "256"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--tolerance", result.ErrorMessage);
    }

    [Fact]
    public void Parse_WhenScreenshotOutputAndRegion_ReturnsOptions()
    {
        var result = _router.Parse(["screenshot", "--output", "/tmp/shot.png", "--region", "1", "2", "30", "40", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotCliOptions>(result.Options);
        Assert.Equal(ScreenshotCliAction.Capture, options.Action);
        Assert.Equal("/tmp/shot.png", options.OutputPath);
        Assert.Equal(1, options.RegionX);
        Assert.Equal(2, options.RegionY);
        Assert.Equal(30, options.RegionWidth);
        Assert.Equal(40, options.RegionHeight);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScreenshotClipboard_ReturnsOptions()
    {
        var result = _router.Parse(["screenshot", "--clipboard", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotCliOptions>(result.Options);
        Assert.True(options.Clipboard);
        Assert.Null(options.OutputPath);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenScreenshotOutputAndClipboard_ReturnsOptions()
    {
        var result = _router.Parse(["screenshot", "-o", "/tmp/shot.png", "--clipboard"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<ScreenshotCliOptions>(result.Options);
        Assert.Equal("/tmp/shot.png", options.OutputPath);
        Assert.True(options.Clipboard);
    }

    [Fact]
    public void Parse_WhenScreenshotMissingOutput_ReturnsError()
    {
        var result = _router.Parse(["screenshot", "--region", "1", "2", "3", "4"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--output", result.ErrorMessage);
        Assert.Contains("--clipboard", result.ErrorMessage);
    }
}
