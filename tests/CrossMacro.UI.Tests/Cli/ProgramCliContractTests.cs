using System.IO;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Tests.Cli;

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

            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);

                var exitCode = CrossMacro.UI.Program.Run(
                    ["--json"],
                    new NoOpPlatformServiceRegistrar(),
                    (_, _) => throw new InvalidOperationException("GUI must not start for CLI parse error."));

                Assert.Equal((int)CliExitCode.InvalidArguments, exitCode);
                Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
                Assert.Contains("\"code\": 2", stdout.ToString(), StringComparison.Ordinal);
                Assert.Equal(string.Empty, stderr.ToString());
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

            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);

                var exitCode = CrossMacro.UI.Program.Run(
                    ["doctor", "--json"],
                    new ThrowingPlatformServiceRegistrar(),
                    (_, _) => throw new InvalidOperationException("GUI must not start for CLI command."));

                Assert.Equal((int)CliExitCode.RuntimeError, exitCode);
                Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
                Assert.Contains("\"code\": 6", stdout.ToString(), StringComparison.Ordinal);
                Assert.Contains("CLI command failed.", stdout.ToString(), StringComparison.Ordinal);
                Assert.Equal(string.Empty, stderr.ToString());
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

            try
            {
                Console.SetOut(stdout);
                Console.SetError(stderr);

                var exitCode = CrossMacro.UI.Program.Run(
                    ["doctor", "--json"],
                    new CancelledPlatformServiceRegistrar(),
                    (_, _) => throw new InvalidOperationException("GUI must not start for CLI command."));

                Assert.Equal((int)CliExitCode.Cancelled, exitCode);
                Assert.Contains("\"status\": \"error\"", stdout.ToString(), StringComparison.Ordinal);
                Assert.Contains("\"code\": 130", stdout.ToString(), StringComparison.Ordinal);
                Assert.Contains("Command cancelled.", stdout.ToString(), StringComparison.Ordinal);
                Assert.Equal(string.Empty, stderr.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
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
}
