// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.Cli.Tests;

public sealed partial class CliCommandRouterTests
{

    [Fact]
    public void Parse_WhenRunWithSteps_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "--step", "move abs 100 100",
            "--step", "click left",
            "--speed", "1.5",
            "--countdown", "2",
            "--timeout", "30",
            "--dry-run",
            "--json",
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
        var result = CliCommandRouterAccessor.Parse(["run", "--file", "/tmp/steps.txt", "--dry-run"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("/tmp/steps.txt", options.StepFilePath);
        Assert.Empty(options.Steps);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_WhenRunWithInlineSteps_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "move", "abs", "100", "200",
            "click", "left",
            "delay", "2.375ms",
            "type", "hello",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(4, options.Steps.Count);
        Assert.Equal("move abs 100 200", options.Steps[0]);
        Assert.Equal("click left", options.Steps[1]);
        Assert.Equal("delay 2.375ms", options.Steps[2]);
        Assert.Equal("type hello", options.Steps[3]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineCurrentPositionClick_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "move", "abs", "100", "200",
            "click", "current", "left",
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
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "set", "n", "2",
            "repeat", "$n", "{",
            "click", "left",
            "delay", "random", "10", "20",
            "}",
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
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "if", "$x", "==", "1", "{",
            "click", "left",
            "}",
            "while", "$i", "<", "3", "{",
            "inc", "i",
            "}",
            "for", "n", "from", "1", "to", "5", "step", "2", "{",
            "click", "right",
            "}",
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
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "repeat", "2", "{",
            "continue",
            "}",
            "for", "i", "from", "1", "to", "2", "{",
            "break",
            "}",
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
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "set", "step", "2",
            "inc", "i", "$step",
            "click", "left",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("set step 2", options.Steps[0]);
        Assert.Equal("inc i $step", options.Steps[1]);
        Assert.Equal("click left", options.Steps[2]);
    }

    [Fact]
    public void Parse_WhenRunWithInlineMulDiv_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "set", "x", "5",
            "mul", "x", "2",
            "div", "x", "$factor",
            "mul", "y",
            "click", "left",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal("set x 5", options.Steps[0]);
        Assert.Equal("mul x 2", options.Steps[1]);
        Assert.Equal("div x $factor", options.Steps[2]);
        Assert.Equal("mul y", options.Steps[3]);
        Assert.Equal("click left", options.Steps[4]);
    }

    [Fact]
    public void Parse_WhenRunWithStepMulDiv_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "--step", "set x 5",
            "--step", "mul x 2",
            "--step", "div x 2",
        ]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["set x 5", "mul x 2", "div x 2"], options.Steps);
    }

    [Theory]
    [InlineData("mul")]
    [InlineData("div")]
    public void Parse_WhenRunWithBareMulDiv_ReturnsError(string command)
    {
        var result = CliCommandRouterAccessor.Parse(["run", command]);

        Assert.False(result.IsSuccess);
        Assert.Contains($"Invalid inline step syntax for {command}. Expected: {command} <name> [amount]", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenRunWithInlineRepeatMissingBrace_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse([
            "run",
            "repeat", "3",
            "click", "left",
        ]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid inline step syntax for repeat", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenRunWithInvalidInlineMove_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "move", "abs", "100"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid inline step syntax for move", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenRunWithoutStep_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "--dry-run"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("run requires at least one --step argument or --file.", result.ErrorMessage);
        Assert.Equal(2, result.ErrorDetails.Count);
        Assert.Contains("Usage: crossmacro run --step <step>", result.ErrorDetails[0], StringComparison.Ordinal);
        Assert.Contains("Usage: crossmacro run <step-command>", result.ErrorDetails[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenRunSpeedBelowRange_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "--step", "click left", "--speed", "0.09"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--speed must be between 0.1 and 10.", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenRunSpeedAboveRange_ReturnsError()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "--step", "click left", "--speed", "10.01"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--speed must be between 0.1 and 10.", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenRunHelp_ReturnsHelpWithTopic()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "--help"]);

        Assert.True(result.ShowHelp);
        Assert.Equal("run", result.HelpTopic);
    }

    [Fact]
    public void GetUsage_WhenRunHelp_IncludesArithmeticSteps()
    {
        var usage = CliCommandRouterAccessor.GetUsage("run");

        Assert.Contains("inc <name> [amount] | dec <name> [amount] | mul <name> [amount] | div <name> [amount]", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void GetUsage_WhenRunHelp_IncludesShellStep()
    {
        var usage = CliCommandRouterAccessor.GetUsage("run");

        Assert.Contains("shell \"<command>\" [retries] [backoff_ms] [timeout_ms]", usage, StringComparison.Ordinal);
        Assert.Contains("shell capture \"<command>\" exit_var stdout_var stderr_var", usage, StringComparison.Ordinal);
        Assert.Contains("shell input \"<stdin text>\" \"<command>\"", usage, StringComparison.Ordinal);
        Assert.Contains("shell capture-input \"<stdin text>\" \"<command>\" exit_var stdout_var stderr_var", usage, StringComparison.Ordinal);
        Assert.Contains("Capture modes do not fail on non-zero exits", usage, StringComparison.Ordinal);
        Assert.Contains("capped at 65536 characters", usage, StringComparison.Ordinal);
        Assert.Contains("only run trusted macros", usage, StringComparison.Ordinal);
        Assert.Contains("Flatpak builds disable shell steps", usage, StringComparison.Ordinal);
        Assert.Contains("Use $$NAME to pass $NAME to the shell", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenInlineShellStepHasOptions_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "shell", "printf ok", "1", "250", "5000"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell \"printf ok\" 1 250 5000"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellStepContainsQuotes_EscapesCommandPayload()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "shell", "printf \"ok\""]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell \"printf \\\"ok\\\"\""], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCaptureStepHasVariables_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "shell", "capture", "printf ok", "code", "out", "err", "1", "250", "5000"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell capture \"printf ok\" code out err 1 250 5000"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellInputStepHasPayload_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "shell", "input", "hello", "cat", "0", "0", "1000"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell input \"hello\" \"cat\" 0 0 1000"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCaptureInputStepHasPayload_ReturnsRunOptions()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "shell", "capture-input", "hello", "cat", "code", "out", "err"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal(["shell capture-input \"hello\" \"cat\" code out err"], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCommandContainsBackslashes_EscapesBackslashes()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "shell", @"printf C:\temp\"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal([@"shell ""printf C:\\temp\\"""], options.Steps);
    }

    [Fact]
    public void Parse_WhenInlineShellCaptureInputContainsBackslashes_EscapesBackslashes()
    {
        var result = CliCommandRouterAccessor.Parse(["run", "shell", "capture-input", @"C:\temp\", "cat", "code", "out", "err"]);

        Assert.True(result.IsSuccess);
        var options = Assert.IsType<RunCliOptions>(result.Options);
        Assert.Equal([@"shell capture-input ""C:\\temp\\"" ""cat"" code out err"], options.Steps);
    }

    [Fact]
    public void GetUsage_WhenRunTopic_ContainsPhase2Details()
    {
        var usage = CliCommandRouterAccessor.GetUsage("run");

        Assert.Contains("--file <steps-file>", usage, StringComparison.Ordinal);
        Assert.Contains("type <text>", usage, StringComparison.Ordinal);
        Assert.Contains("break | continue", usage, StringComparison.Ordinal);
        Assert.Contains("Examples:", usage, StringComparison.Ordinal);
    }
}
