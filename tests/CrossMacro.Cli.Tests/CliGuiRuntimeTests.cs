using System.Text.Json;

namespace CrossMacro.Cli.Tests;

public sealed class CliGuiRuntimeTests
{
    private const string UsageHint = "See crossmacro --help for usage information.";

    [Fact]
    public async Task RunAsync_WhenStandaloneJsonFlagWithoutCommand_ReturnsInvalidArgumentsAsJson()
    {
        var run = await RunAsync(["--json"]);

        Assert.Equal((int)CliExitCode.InvalidArguments, run.ExitCode);
        using var document = JsonDocument.Parse(run.Stdout);
        Assert.Equal("error", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("code").GetInt32());
        Assert.Equal("Option --json requires a command.", document.RootElement.GetProperty("message").GetString());
        Assert.Equal(UsageHint, document.RootElement.GetProperty("errors")[0].GetString());
        Assert.DoesNotContain("\\u0027", run.Stdout, StringComparison.Ordinal);
        AssertNoUnexpectedStderr(run.Stderr);
    }

    [Fact]
    public async Task RunAsync_WhenCommandParseFailsWithJsonFlag_ReturnsInvalidArgumentsAsJson()
    {
        var run = await RunAsync(["doctor", "--bad", "--json"]);

        Assert.Equal((int)CliExitCode.InvalidArguments, run.ExitCode);
        using var document = JsonDocument.Parse(run.Stdout);
        Assert.Equal("error", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("code").GetInt32());
        var message = document.RootElement.GetProperty("message").GetString();
        Assert.Equal("Unknown option for doctor: --bad", message);
        Assert.DoesNotContain("Usage:", message, StringComparison.Ordinal);
        Assert.Equal(UsageHint, document.RootElement.GetProperty("errors")[0].GetString());
        Assert.DoesNotContain("\\u0027", run.Stdout, StringComparison.Ordinal);
        AssertNoUnexpectedStderr(run.Stderr);
    }

    [Fact]
    public async Task RunAsync_WhenCommandParseNeedsUsageWithJsonFlag_UsesConciseMessageAndDetailedErrors()
    {
        var run = await RunAsync(["run", "--json"]);

        Assert.Equal((int)CliExitCode.InvalidArguments, run.ExitCode);
        using var document = JsonDocument.Parse(run.Stdout);
        Assert.Equal("run requires at least one --step argument or --file.", document.RootElement.GetProperty("message").GetString());
        var errors = document.RootElement.GetProperty("errors");
        Assert.Equal(2, errors.GetArrayLength());
        Assert.Contains("Usage: crossmacro run --step <step>", errors[0].GetString(), StringComparison.Ordinal);
        Assert.Contains("Usage: crossmacro run <step-command>", errors[1].GetString(), StringComparison.Ordinal);
        AssertNoUnexpectedStderr(run.Stderr);
    }

    [Fact]
    public async Task RunAsync_WhenRuntimeExceptionWithJsonOption_ReturnsRuntimeErrorAsJson()
    {
        var run = await RunAsync(
            ["doctor", "--json"],
            registerPlatform: static _ => throw new InvalidOperationException("simulated registration failure"));

        Assert.Equal((int)CliExitCode.RuntimeError, run.ExitCode);
        Assert.Contains("\"status\": \"error\"", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("\"code\": 6", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("CLI command failed.", run.Stdout, StringComparison.Ordinal);
        AssertNoUnexpectedStderr(run.Stderr);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringCliBootstrap_ReturnsCancelledAsJson()
    {
        var run = await RunAsync(
            ["doctor", "--json"],
            registerPlatform: static _ => throw new OperationCanceledException("simulated cancellation"));

        Assert.Equal((int)CliExitCode.Cancelled, run.ExitCode);
        Assert.Contains("\"status\": \"error\"", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("\"code\": 130", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("Command cancelled.", run.Stdout, StringComparison.Ordinal);
        AssertNoUnexpectedStderr(run.Stderr);
    }

    [Fact]
    public async Task RunAsync_WhenHeadlessAndSingleInstanceGuardUnavailable_ReturnsEnvironmentErrorAsJson()
    {
        var run = await RunAsync(["headless", "--json"], tryAcquireSingleInstanceGuard: static () => null);

        Assert.Equal((int)CliExitCode.EnvironmentError, run.ExitCode);
        Assert.Contains("\"status\": \"error\"", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("\"code\": 5", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("Another CrossMacro runtime instance is already running.", run.Stdout, StringComparison.Ordinal);
        AssertNoUnexpectedStderr(run.Stderr);
    }

    [Fact]
    public async Task RunAsync_WhenGuiModeAndSingleInstanceGuardUnavailable_DoesNotStartGuiAndReturnsEnvironmentError()
    {
        var guiStarted = false;

        var run = await RunAsync(
            [],
            tryAcquireSingleInstanceGuard: static () => null,
            startGui: () =>
            {
                guiStarted = true;
                return 0;
            });

        Assert.Equal((int)CliExitCode.EnvironmentError, run.ExitCode);
        Assert.False(guiStarted);
    }

    [Fact]
    public async Task RunAsync_WhenExistingGuiInstanceActivates_ReturnsSuccessWithoutStartingGui()
    {
        var guiStarted = false;
        var activationRequested = false;

        var run = await RunAsync(
            [],
            tryAcquireSingleInstanceGuard: static () => null,
            startGui: () =>
            {
                guiStarted = true;
                return 0;
            },
            tryActivateExistingInstance: () =>
            {
                activationRequested = true;
                return true;
            });

        Assert.Equal((int)CliExitCode.Success, run.ExitCode);
        Assert.True(activationRequested);
        Assert.False(guiStarted);
    }

    private static async Task<CliRun> RunAsync(
        string[] args,
        Action<IServiceCollection>? registerPlatform = null,
        Func<IDisposable?>? tryAcquireSingleInstanceGuard = null,
        Func<int>? startGui = null,
        Func<bool>? tryActivateExistingInstance = null)
    {
        registerPlatform ??= static _ => { };
        tryAcquireSingleInstanceGuard ??= static () => new NoOpGuard();
        startGui ??= static () => throw new InvalidOperationException("GUI must not start in this scenario.");

        using var console = new ConsoleCapture();

        var exitCode = await CliGuiRuntime.RunAsync(
            args,
            registerPlatform,
            (services, _) => registerPlatform(services),
            startGui,
            getVersionString: static () => "CrossMacro 0.0.0",
            tryAcquireSingleInstanceGuard,
            bootstrapCallbacks: CliBootstrapCallbacks.NoOp,
            tryActivateExistingInstance);

        return new CliRun(exitCode, console.StandardOutput, console.StandardError);
    }

    private static void AssertNoUnexpectedStderr(string stderr)
    {
        var normalized = stderr.Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Assert.StartsWith("[CrossMacro]", line, StringComparison.Ordinal);
        }
    }

    private sealed record CliRun(int ExitCode, string Stdout, string Stderr);

    private sealed class NoOpGuard : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
