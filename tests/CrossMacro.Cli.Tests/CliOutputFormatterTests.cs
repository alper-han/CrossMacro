
namespace CrossMacro.Cli.Tests;

public sealed class CliOutputFormatterTests
{
    [Fact]
    public void Write_WhenJsonOutput_AlwaysWritesToStdout()
    {
        using var console = new ConsoleCapture();
        var result = OperationResult.Fail(
            ExecutionOutcomeCode.RuntimeError,
            "runtime failed",
            errors: ["boom"]);

        CliOutputFormatter.Write(result, jsonOutput: true);

        var envelope = JsonNode.Parse(console.StandardOutput)!.AsObject();
        Assert.Equal("error", (string?)envelope["status"]);
        Assert.Equal((int)ExecutionOutcomeCode.RuntimeError, (int?)envelope["code"]);
        Assert.Equal("runtime failed", (string?)envelope["message"]);
        Assert.Equal("boom", (string?)envelope["errors"]![0]);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public void Write_WhenTextSuccess_WritesToStdout()
    {
        using var console = new ConsoleCapture();

        CliOutputFormatter.Write(OperationResult.Ok("done"), jsonOutput: false);

        Assert.Contains("Status: ok", console.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Code: 0", console.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Message: done", console.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public void Write_WhenTextFailure_WritesToStderr()
    {
        using var console = new ConsoleCapture();
        var result = OperationResult.Fail(
            ExecutionOutcomeCode.InvalidArguments,
            "bad args",
            errors: ["unknown option"]);

        CliOutputFormatter.Write(result, jsonOutput: false);

        Assert.Equal(string.Empty, console.StandardOutput);
        Assert.Contains("Status: error", console.StandardError, StringComparison.Ordinal);
        Assert.Contains("Code: 2", console.StandardError, StringComparison.Ordinal);
        Assert.Contains("Message: bad args", console.StandardError, StringComparison.Ordinal);
        Assert.Contains("- unknown option", console.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WhenTextSuccessWithData_WritesDataAsText()
    {
        using var console = new ConsoleCapture();
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

        CliOutputFormatter.Write(OperationResult.Ok("macro info loaded", data: data), jsonOutput: false);

        var output = console.StandardOutput;
        Assert.Contains("Data:", output, StringComparison.Ordinal);
        Assert.Contains("macroPath: /tmp/demo.macro", output, StringComparison.Ordinal);
        Assert.Contains("eventCount: 12", output, StringComparison.Ordinal);
        Assert.Contains("eventBreakdown:", output, StringComparison.Ordinal);
        Assert.Contains("mouseMove: 3", output, StringComparison.Ordinal);
        Assert.Contains("- demo", output, StringComparison.Ordinal);
        Assert.Contains("- smoke", output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, console.StandardError);
    }
}
