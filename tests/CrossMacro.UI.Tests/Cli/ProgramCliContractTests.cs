using System;
using System.IO;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Tests.Cli;

[Collection("EnvironmentVariableSensitive")]
public class ProgramCliContractTests
{
    [Fact]
    public void Run_WhenStandaloneJsonFlagWithoutCommand_ReturnsInvalidArgumentsAsJson()
    {
        lock (ConsoleTestLock.Gate)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            using var dataHome = new TemporaryDataHomeScope();

            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);

                var exitCode = CliGuiRuntime.Run(
                    ["--json"],
                    new NoOpPlatformServiceRegistrar(),
                    startGui: () => throw new InvalidOperationException("GUI must not start for CLI parse error."),
                    getVersionString: () => "CrossMacro 0.0.0",
                    tryAcquireSingleInstanceGuard: static () => new NoOpGuard());

                Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
                Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
                Assert.Contains("\"code\": 2", stdout.ToString(), StringComparison.Ordinal);
                AssertNoUnexpectedStderr(stderr.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }

    [Fact]
    public void Run_WhenRuntimeExceptionWithJsonOption_ReturnsRuntimeErrorAsJson()
    {
        lock (ConsoleTestLock.Gate)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            using var dataHome = new TemporaryDataHomeScope();

            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);

                var exitCode = CliGuiRuntime.Run(
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
    }

    [Fact]
    public void Run_WhenCancelledDuringCliBootstrap_ReturnsCancelledAsJson()
    {
        lock (ConsoleTestLock.Gate)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            using var dataHome = new TemporaryDataHomeScope();

            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);

                var exitCode = CliGuiRuntime.Run(
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
    }

    [Fact]
    public void Run_WhenHeadlessAndSingleInstanceGuardUnavailable_ReturnsEnvironmentErrorAsJson()
    {
        lock (ConsoleTestLock.Gate)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            using var dataHome = new TemporaryDataHomeScope();

            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);

                var exitCode = CliGuiRuntime.Run(
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
    }

    [Fact]
    public void Run_WhenGuiModeAndSingleInstanceGuardUnavailable_DoesNotStartGuiAndReturnsEnvironmentError()
    {
        var guiStarted = false;
        using var dataHome = new TemporaryDataHomeScope();

        var exitCode = CliGuiRuntime.Run(
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
        public void RegisterPlatformServices(IServiceCollection services)
        {
        }
    }

    private sealed class ThrowingPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services)
        {
            throw new InvalidOperationException("simulated registration failure");
        }
    }

    private sealed class CancelledPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
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
