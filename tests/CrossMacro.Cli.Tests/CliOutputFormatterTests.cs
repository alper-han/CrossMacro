
namespace CrossMacro.Cli.Tests;

public sealed class CliOutputFormatterTests
{
    [Fact]
    public async Task Write_WhenJsonOutput_AlwaysWritesToStdout()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = CliCommandExecutionResult.Fail(
                CliExitCode.RuntimeError,
                "runtime failed",
                errors: ["boom"]);

            CliOutputFormatter.Write(result, jsonOutput: true);

            Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"code\": 6", stdout.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Write_WhenJsonOutputContainsQuickSetupData_UsesStablePropertyNames()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = CliCommandExecutionResult.Ok(
                "quick setup completed",
                new QuickSetupCommandData("flatpak", Applicable: true, Applied: true));

            CliOutputFormatter.Write(result, jsonOutput: true);

            var output = stdout.ToString();
            Assert.Contains("\"provider\": \"flatpak\"", output, StringComparison.Ordinal);
            Assert.Contains("\"applicable\": true", output, StringComparison.Ordinal);
            Assert.Contains("\"applied\": true", output, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Provider\"", output, StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Write_WhenShortcutListContainsWindowRules_UsesStablePropertyNames()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var task = new ShortcutTaskData(
                Guid.Empty,
                "Browser shortcut",
                Enabled: true,
                Hotkey: "Ctrl+Alt+B",
                MacroFilePath: "/tmp/browser.macro",
                PlaybackSpeed: 1,
                LoopEnabled: false,
                RunWhileHeld: false,
                RepeatCount: 0,
                RepeatDelayMs: 0,
                RandomRepeatDelay: false,
                RepeatDelayMinMs: null,
                RepeatDelayMaxMs: null,
                WindowRules:
                [
                    new ShortcutWindowRule
                    {
                        Field = TriggerField.WindowClass,
                        MatchMode = TriggerMatchMode.Equals,
                        Value = "org.mozilla.firefox",
                    },
                ],
                LastTriggeredTime: null,
                LastStatus: null);
            var result = CliCommandExecutionResult.Ok(
                "shortcut list loaded",
                new TaskListData<ShortcutTaskData>(1, [task]));

            CliOutputFormatter.Write(result, jsonOutput: true);

            var output = stdout.ToString();
            Assert.Contains("\"windowRules\": [", output, StringComparison.Ordinal);
            Assert.Contains("\"field\": 0", output, StringComparison.Ordinal);
            Assert.Contains("\"matchMode\": 0", output, StringComparison.Ordinal);
            Assert.Contains("\"value\": \"org.mozilla.firefox\"", output, StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Write_WhenJsonOutputContainsBackticks_WritesReadableBackticks()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var data = new JsonObject { ["remediation"] = "Run `sudo systemctl restart crossmacro.service`." };
            var result = CliCommandExecutionResult.Ok("doctor ready", data: data);

            CliOutputFormatter.Write(result, jsonOutput: true);

            var output = stdout.ToString();
            Assert.Contains("Run `sudo systemctl restart crossmacro.service`.", output, StringComparison.Ordinal);
            Assert.DoesNotContain("\\u0060", output, StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Write_WhenTextSuccess_WritesToStdout()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = CliCommandExecutionResult.Ok("done");
            CliOutputFormatter.Write(result, jsonOutput: false);

            Assert.Contains("Status: ok", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("Code: 0", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("Message: done", stdout.ToString(), StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Write_WhenTextFailure_WritesToStderr()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "bad args",
                errors: ["unknown option"]);
            CliOutputFormatter.Write(result, jsonOutput: false);

            Assert.Equal(string.Empty, stdout.ToString());
            Assert.Contains("Status: error", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("Code: 2", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("Message: bad args", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("Errors:", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("- unknown option", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task Write_WhenTextSuccessWithData_WritesDataAsText()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var data = new JsonObject
            {
                ["macroPath"] = "/tmp/demo.macro",
                ["eventCount"] = 12,
                ["eventBreakdown"] = new JsonObject
                {
                    ["mouseMove"] = 3,
                    ["click"] = 4,
                },
                ["tags"] = new JsonArray("demo", "smoke"),
            };
            var result = CliCommandExecutionResult.Ok("macro info loaded", data: data);
            CliOutputFormatter.Write(result, jsonOutput: false);

            var output = stdout.ToString();
            Assert.Contains("Data:", output, StringComparison.Ordinal);
            Assert.Contains("macroPath: /tmp/demo.macro", output, StringComparison.Ordinal);
            Assert.Contains("eventCount: 12", output, StringComparison.Ordinal);
            Assert.Contains("eventBreakdown:", output, StringComparison.Ordinal);
            Assert.Contains("mouseMove: 3", output, StringComparison.Ordinal);
            Assert.Contains("tags:", output, StringComparison.Ordinal);
            Assert.Contains("- demo", output, StringComparison.Ordinal);
            Assert.Contains("- smoke", output, StringComparison.Ordinal);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
