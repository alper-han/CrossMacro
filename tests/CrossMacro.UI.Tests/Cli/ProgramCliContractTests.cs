using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Cli;
using CrossMacro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Tests.Cli;

[Collection("EnvironmentVariableSensitive")]
public class ProgramCliContractTests
{
    [Fact]
    public async Task RunAsync_WhenStandaloneJsonFlagWithoutCommand_ReturnsInvalidArgumentsAsJson()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var dataHome = new TemporaryDataHomeScope();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await CliGuiRuntime.RunAsync(
                ["--json"],
                new NoOpPlatformServiceRegistrar(),
                startGui: () => throw new InvalidOperationException("GUI must not start for CLI parse error."),
                getVersionString: () => "CrossMacro 0.0.0",
                tryAcquireSingleInstanceGuard: static () => new NoOpGuard());

            Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
            using var document = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("error", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("Option --json requires a command.", document.RootElement.GetProperty("message").GetString());
            Assert.Equal("See crossmacro --help for usage information.", document.RootElement.GetProperty("errors")[0].GetString());
            Assert.DoesNotContain("\\u0027", stdout.ToString(), StringComparison.Ordinal);
            AssertNoUnexpectedStderr(stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenCommandParseFailsWithJsonFlag_ReturnsInvalidArgumentsAsJson()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var dataHome = new TemporaryDataHomeScope();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await CliGuiRuntime.RunAsync(
                ["doctor", "--bad", "--json"],
                new NoOpPlatformServiceRegistrar(),
                startGui: () => throw new InvalidOperationException("GUI must not start for CLI parse error."),
                getVersionString: () => "CrossMacro 0.0.0",
                tryAcquireSingleInstanceGuard: static () => new NoOpGuard());

            Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
            using var document = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("error", document.RootElement.GetProperty("status").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("code").GetInt32());
            Assert.Equal("Unknown option for doctor: --bad", document.RootElement.GetProperty("message").GetString());
            Assert.Equal("See crossmacro --help for usage information.", document.RootElement.GetProperty("errors")[0].GetString());
            Assert.DoesNotContain("Usage:", document.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.DoesNotContain("\\u0027", stdout.ToString(), StringComparison.Ordinal);
            AssertNoUnexpectedStderr(stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenCommandParseNeedsUsageWithJsonFlag_UsesConciseMessageAndDetailedErrors()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var dataHome = new TemporaryDataHomeScope();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await CliGuiRuntime.RunAsync(
                ["run", "--json"],
                new NoOpPlatformServiceRegistrar(),
                startGui: () => throw new InvalidOperationException("GUI must not start for CLI parse error."),
                getVersionString: () => "CrossMacro 0.0.0",
                tryAcquireSingleInstanceGuard: static () => new NoOpGuard());

            Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
            using var document = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("run requires at least one --step argument or --file.", document.RootElement.GetProperty("message").GetString());
            var errors = document.RootElement.GetProperty("errors");
            Assert.Equal(2, errors.GetArrayLength());
            Assert.Contains("Usage: crossmacro run --step <step>", errors[0].GetString(), StringComparison.Ordinal);
            Assert.Contains("Usage: crossmacro run <step-command>", errors[1].GetString(), StringComparison.Ordinal);
            AssertNoUnexpectedStderr(stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenRuntimeExceptionWithJsonOption_ReturnsRuntimeErrorAsJson()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var dataHome = new TemporaryDataHomeScope();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await CliGuiRuntime.RunAsync(
                ["doctor", "--json"],
                new ThrowingPlatformServiceRegistrar(),
                startGui: () => throw new InvalidOperationException("GUI must not start for CLI command."),
                getVersionString: () => "CrossMacro 0.0.0",
                tryAcquireSingleInstanceGuard: static () => new NoOpGuard());

            Assert.Equal((int)CliExitCode.RuntimeError, exitCode);
            Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"code\": 6", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("CLI command failed.", stdout.ToString(), StringComparison.Ordinal);
            AssertNoUnexpectedStderr(stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringCliBootstrap_ReturnsCancelledAsJson()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var dataHome = new TemporaryDataHomeScope();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await CliGuiRuntime.RunAsync(
                ["doctor", "--json"],
                new CancelledPlatformServiceRegistrar(),
                startGui: () => throw new InvalidOperationException("GUI must not start for CLI command."),
                getVersionString: () => "CrossMacro 0.0.0",
                tryAcquireSingleInstanceGuard: static () => new NoOpGuard());

            Assert.Equal((int)CliExitCode.Cancelled, exitCode);
            Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"code\": 130", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("Command cancelled.", stdout.ToString(), StringComparison.Ordinal);
            AssertNoUnexpectedStderr(stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenHeadlessAndSingleInstanceGuardUnavailable_ReturnsEnvironmentErrorAsJson()
    {
        using var consoleLock = await ConsoleTestLock.AcquireAsync();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var dataHome = new TemporaryDataHomeScope();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var exitCode = await CliGuiRuntime.RunAsync(
                ["headless", "--json"],
                new NoOpPlatformServiceRegistrar(),
                startGui: () => throw new InvalidOperationException("GUI must not start for headless command."),
                getVersionString: () => "CrossMacro 0.0.0",
                tryAcquireSingleInstanceGuard: static () => null);

            Assert.Equal((int)CliExitCode.EnvironmentError, exitCode);
            Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"code\": 5", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("Another CrossMacro runtime instance is already running.", stdout.ToString(), StringComparison.Ordinal);
            AssertNoUnexpectedStderr(stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunAsync_WhenGuiModeAndSingleInstanceGuardUnavailable_DoesNotStartGuiAndReturnsEnvironmentError()
    {
        var guiStarted = false;
        using var dataHome = new TemporaryDataHomeScope();

        var exitCode = await CliGuiRuntime.RunAsync(
            [],
            new NoOpPlatformServiceRegistrar(),
            startGui: () =>
            {
                guiStarted = true;
                return 0;
            },
            getVersionString: () => "CrossMacro 0.0.0",
            tryAcquireSingleInstanceGuard: static () => null);

        Assert.Equal((int)CliExitCode.EnvironmentError, exitCode);
        Assert.False(guiStarted);
    }

    private sealed class NoOpPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public void RegisterPlatformServices(IServiceCollection services)
        {
        }
    }

    private sealed class ThrowingPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public void RegisterPlatformServices(IServiceCollection services)
        {
            throw new InvalidOperationException("simulated registration failure");
        }
    }

    private sealed class CancelledPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Default;

        public void RegisterPlatformServices(IServiceCollection services)
        {
            throw new OperationCanceledException("simulated cancellation");
        }
    }

    private sealed class NoOpGuard : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private static void AssertNoUnexpectedStderr(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return;
        }

        var normalized = stderr.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            Assert.StartsWith("[CrossMacro]", line, StringComparison.Ordinal);
        }
    }

    private sealed class TemporaryDataHomeScope : IDisposable
    {
        private readonly string _tempDir;
        private readonly string? _previousValue;

        public TemporaryDataHomeScope()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "crossmacro-tests", nameof(ProgramCliContractTests), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _previousValue = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", _tempDir);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", _previousValue);
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
