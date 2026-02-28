using System.IO;
using CrossMacro.Cli;

namespace CrossMacro.Cli.Tests;

public class CliOutputFormatterTests
{
    [Fact]
    public void Write_WhenJsonOutput_AlwaysWritesToStdout()
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
    }

    [Fact]
    public void Write_WhenTextSuccess_WritesToStdout()
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
    }

    [Fact]
    public void Write_WhenTextFailure_WritesToStderr()
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
    }

    [Fact]
    public void Write_WhenTextSuccessWithData_WritesDataAsText()
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

                var result = CliCommandExecutionResult.Ok(
                    "macro info loaded",
                    data: new
                    {
                        macroPath = "/tmp/demo.macro",
                        eventCount = 12,
                        eventBreakdown = new
                        {
                            mouseMove = 3,
                            click = 4
                        },
                        tags = new[] { "demo", "smoke" }
                    });
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
}
