using CrossMacro.Cli.Parsing;

namespace CrossMacro.Cli.Tests;

public sealed partial class CliCommandRouterTests
{

    [Fact]
    public void Parse_WhenNoArgs_StartsGui()
    {
        var result = CliCommandRouterAccessor.Parse([]);

        Assert.True(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        Assert.False(result.ShowHelp);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_WhenVersionToken_ReturnsVersion()
    {
        var result = CliCommandRouterAccessor.Parse(["--version"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.ShowVersion);
        Assert.False(result.ShouldStartGui);
        Assert.Null(result.Options);
    }

    [Fact]
    public void Parse_WhenMacroValidateWithJson_ReturnsOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["macro", "validate", "/tmp/test.macro", "--json"]);

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
        var result = CliCommandRouterAccessor.Parse(["macro", "info", "/tmp/test.macro", "--json"]);

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
        var result = CliCommandRouterAccessor.Parse(["macro", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("macro", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenMacroValidateHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["macro", "validate", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("macro.validate", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenUnknownOption_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["macro", "validate", "/tmp/test.macro", "--bad"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown option", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenUnknownTokenWithoutCliPrefix_ReturnsUnknownCommandError()
    {
        var result = CliCommandRouterAccessor.Parse(["some-random-token"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown command", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenKnownGuiDashedToken_StartsGui()
    {
        var result = CliCommandRouterAccessor.Parse(["--drm"]);

        Assert.True(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parse_WhenUnknownDashedToken_ReturnsUnknownOptionError()
    {
        var result = CliCommandRouterAccessor.Parse(["--unknown-switch"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown option", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenKnownGuiDisplaySwitchWithValue_StartsGui()
    {
        var result = CliCommandRouterAccessor.Parse(["--display=:0"]);

        Assert.True(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parse_WhenStartMinimizedGuiFlag_StartsGui()
    {
        var result = CliCommandRouterAccessor.Parse(["--start-minimized"]);

        Assert.True(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Parse_WhenExistingPathToken_ReturnsUnknownCommandError()
    {
        var path = Path.GetTempFileName();
        try
        {
            var result = CliCommandRouterAccessor.Parse([path]);

            Assert.False(result.ShouldStartGui);
            Assert.False(result.IsSuccess);
            Assert.Contains("Unknown command", result.ErrorMessage, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Parse_WhenStandaloneJsonFlagWithoutCommand_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.True(result.PrefersJsonOutput);
        Assert.Equal("Option --json requires a command.", result.ErrorMessage);
        Assert.Contains("See crossmacro --help for usage information.", result.ErrorDetails, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_WhenCommandParseFailsAfterJsonFlag_PrefersJsonOutput()
    {
        var result = CliCommandRouterAccessor.Parse(["doctor", "--bad", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.True(result.PrefersJsonOutput);
        Assert.Contains("Unknown option for doctor", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenStandaloneLogLevelFlagWithoutCommand_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["--log-level", "debug"]);

        Assert.False(result.ShouldStartGui);
        Assert.False(result.IsSuccess);
        Assert.Contains("requires a command", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WhenPlayWithOptions_ReturnsPlayOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--speed", "1.5", "--motion-mode", "strict-speed", "--motion-rate", "240", "--precision-motion-rate", "320", "--motion-error-px", "1.25", "--loop", "--repeat", "3", "--repeat-delay-ms", "200", "--countdown", "1", "--timeout", "30", "--dry-run", "--json"]);

        Assert.False(result.ShouldStartGui);
        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.Equal("/tmp/test.macro", options.MacroFilePath);
        Assert.Equal(1.5, options.SpeedMultiplier);
        Assert.Equal(MotionPlaybackMode.StrictSpeed, options.MotionMode);
        Assert.Equal(240, options.StrictSpeedMotionEventsPerSecond);
        Assert.Equal(320, options.PrecisionMotionEventsPerSecond);
        Assert.Equal(1.25d, options.MaximumMotionErrorPixels);
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
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--detach"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown option for play", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenPlayWithLogLevel_ReturnsNormalizedLevel()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenPlayRepeatWithoutLoop_EnablesLoopSemantics()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--repeat", "50"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.True(options.Loop);
        Assert.Equal(50, options.RepeatCount);
    }

    [Fact]
    public void Parse_WhenPlayLoopWithoutRepeat_UsesInfiniteLoopDefaults()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--loop"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.True(options.Loop);
        Assert.Equal(0, options.RepeatCount);
    }

    [Fact]
    public void Parse_WhenPlayRepeatZeroWithoutLoop_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--repeat", "0"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("requires --loop", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WhenPlayRepeatZeroWithLoop_IsAllowed()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--loop", "--repeat", "0"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<PlayCliOptions>(result.Options);
        Assert.True(options.Loop);
        Assert.Equal(0, options.RepeatCount);
    }

    [Fact]
    public void Parse_WhenPlayHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("play", result.HelpTopic);
    }

    [Fact]
    public void Parse_WhenPlayHasInvalidRepeat_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["play", "/tmp/test.macro", "--repeat", "-2"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--repeat must be >= 0", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenDoctorWithVerboseAndJson_ReturnsDoctorOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["doctor", "--verbose", "--json"]);

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
        var result = CliCommandRouterAccessor.Parse(["doctor", "--log-level", "trace"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid value for --log-level", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenSetupWithJson_ReturnsQuickSetupOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["setup", "--json"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<QuickSetupCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
    }

    [Fact]
    public void Parse_WhenQuickSetupAliasIsUsed_ReturnsQuickSetupOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["quick-setup"]);

        Assert.True(result.IsSuccess);
        _ = Assert.IsType<QuickSetupCliOptions>(result.Options);
    }

    [Fact]
    public void Parse_WhenRecordWithAllOptions_ReturnsRecordOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "record",
            "--output", "/tmp/out.macro",
            "--mouse", "true",
            "--keyboard", "false",
            "--mode", "absolute",
            "--skip-initial-zero",
            "--duration", "10",
            "--json",
            "--log-level", "warning",
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
    public void Parse_WhenRecordUsesOnlyOutput_PreservesOptionDefaults()
    {
        var result = CliCommandRouterAccessor.Parse(["record", "--output", "recorded.macro"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RecordCliOptions>(result.Options);
        Assert.Equal("recorded.macro", options.OutputFilePath);
        Assert.True(options.RecordMouse);
        Assert.True(options.RecordKeyboard);
        Assert.Equal(RecordCoordinateMode.Auto, options.CoordinateMode);
        Assert.False(options.SkipInitialZero);
        Assert.Equal(0, options.DurationSeconds);
        Assert.False(options.JsonOutput);
        Assert.Null(options.LogLevel);
    }

    [Fact]
    public void Parse_WhenRecordMissingOutput_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["record", "--mode", "auto"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("record requires --output <macro-file>.", result.ErrorMessage);
        Assert.Contains("Usage: crossmacro record (--output|-o) <macro-file>", result.ErrorDetails[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenRecordModeInvalid_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["record", "--output", "/tmp/a.macro", "--mode", "invalid"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid value for --mode", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenHeadlessCommand_ReturnsHeadlessOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["headless", "--json", "--log-level", "debug"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<HeadlessCliOptions>(result.Options);
        Assert.True(options.JsonOutput);
        Assert.Equal("Debug", options.LogLevel);
    }

    [Fact]
    public void Parse_WhenHeadlessUsesDefaults_PreservesFalseAndNullValues()
    {
        var result = CliCommandRouterAccessor.Parse(["headless"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<HeadlessCliOptions>(result.Options);
        Assert.False(options.JsonOutput);
        Assert.Null(options.LogLevel);
    }

    [Fact]
    public void Parse_WhenDoctorUsesCaseInsensitiveVerbose_PreservesDefaults()
    {
        var result = CliCommandRouterAccessor.Parse(["doctor", "--VeRbOsE"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<DoctorCliOptions>(result.Options);
        Assert.True(options.Verbose);
        Assert.False(options.JsonOutput);
        Assert.Null(options.LogLevel);
    }

    [Fact]
    public void Parse_WhenHeadlessFlag_ReturnsHeadlessOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["--headless"]);

        Assert.True(result.IsSuccess);
        _ = Assert.IsType<HeadlessCliOptions>(result.Options);
    }

    [Fact]
    public void GetUsage_WhenDefault_ContainsRecordShortOptionAndHeadlessAlias()
    {
        var usage = CliCommandRouterAccessor.GetUsage();

        Assert.Contains("crossmacro [--start-minimized]", usage, StringComparison.Ordinal);
        Assert.Contains("record (--output|-o)", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro clipboard get", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro clipboard clear", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro window active|list", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro screen pixel|wait-color|search-color|search-image|wait-image|image-click", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro screenshot", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro profile", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro setup", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro text-expansion", usage, StringComparison.Ordinal);
        Assert.Contains("crossmacro --headless", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseHelpers_HasJsonOption_RespectsCaseAndStartIndex()
    {
        var arguments = new[] { "--json", "command", "--JSON" };

        Assert.True(CliParseHelpers.HasJsonOption(arguments, 0));
        Assert.True(CliParseHelpers.HasJsonOption(arguments, 2));
        Assert.False(CliParseHelpers.HasJsonOption(arguments, 3));
        Assert.True(CliParseHelpers.HasJsonOption(arguments, -10));
    }

    [Fact]
    public void ParseHelpers_ReadersUseInvariantValuesAndAdvanceTheIndex()
    {
        var integerArguments = new[] { "--count", "-42" };
        var integerIndex = 0;
        Assert.True(CliParseHelpers.TryReadInt(integerArguments, ref integerIndex, out var integer, out var integerError));
        Assert.Equal(-42, integer);
        Assert.Empty(integerError);
        Assert.Equal(1, integerIndex);

        var doubleArguments = new[] { "--ratio", "1.5" };
        var doubleIndex = 0;
        Assert.True(CliParseHelpers.TryReadDouble(doubleArguments, ref doubleIndex, out var ratio, out var doubleError));
        Assert.Equal(1.5d, ratio);
        Assert.Empty(doubleError);
        Assert.Equal(1, doubleIndex);

        var boolArguments = new[] { "--enabled", "yes" };
        var boolIndex = 0;
        Assert.True(CliParseHelpers.TryReadBool(boolArguments, ref boolIndex, out var enabled, out var boolError));
        Assert.True(enabled);
        Assert.Empty(boolError);
        Assert.Equal(1, boolIndex);

        var modeArguments = new[] { "--mode", "rel" };
        var modeIndex = 0;
        Assert.True(CliParseHelpers.TryReadRecordMode(modeArguments, ref modeIndex, out var mode, out var modeError));
        Assert.Equal(RecordCoordinateMode.Relative, mode);
        Assert.Empty(modeError);
        Assert.Equal(1, modeIndex);
    }

    [Fact]
    public void ParseHelpers_CommonOptions_PreservesHelpAndJsonErrorContracts()
    {
        var logArguments = new[] { "--log-level", "debug" };
        var logIndex = 0;
        var jsonOutput = false;
        string? logLevel = null;

        Assert.True(CliParseHelpers.TryHandleCommonCliOption(
            logArguments,
            ref logIndex,
            "doctor",
            ref jsonOutput,
            ref logLevel,
            out var logResult));
        Assert.Null(logResult);
        Assert.Equal("Debug", logLevel);
        Assert.Equal(1, logIndex);

        var invalidArguments = new[] { "--log-level", "invalid", "--json" };
        var invalidIndex = 0;
        Assert.True(CliParseHelpers.TryHandleCommonCliOption(
            invalidArguments,
            ref invalidIndex,
            "doctor",
            ref jsonOutput,
            ref logLevel,
            out var invalidResult));
        Assert.NotNull(invalidResult);
        Assert.True(invalidResult.PrefersJsonOutput);

        var helpArguments = new[] { "--help" };
        var helpIndex = 0;
        Assert.True(CliParseHelpers.TryHandleCommonCliOption(
            helpArguments,
            ref helpIndex,
            "doctor",
            ref jsonOutput,
            ref logLevel,
            out var helpResult));
        Assert.True(helpResult!.ShowHelp);
        Assert.Equal("doctor", helpResult.HelpTopic);
    }

    [Fact]
    public void ValueContracts_PreserveRecordModesAndWindowSelectorData()
    {
        Assert.Equal(0, (int)RecordCoordinateMode.Auto);
        Assert.Equal(1, (int)RecordCoordinateMode.Absolute);
        Assert.Equal(2, (int)RecordCoordinateMode.Relative);

        var selector = new WindowSelector(WindowSelectorKind.Title, "CrossMacro");

        Assert.Equal(WindowSelectorKind.Title, selector.Kind);
        Assert.Equal("CrossMacro", selector.Value);
        Assert.Equal(selector, new WindowSelector(WindowSelectorKind.Title, "CrossMacro"));
    }
}
