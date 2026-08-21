namespace CrossMacro.Mcp.Tests;

public sealed class McpToolOutcomeMapperTests
{
    [Fact]
    public void FromCliResult_WhenSuccessful_ShouldPreserveTheMessageAndWarnings()
    {
        var outcome = McpToolOutcomeMapper.FromCliResult(
            CliCommandExecutionResult.Ok("Completed.", warnings: ["A warning."]));

        Assert.True(outcome.Success);
        Assert.Equal((int)CliExitCode.Success, outcome.ExitCode);
        Assert.Equal("Completed.", outcome.Message);
        Assert.Equal(["A warning."], outcome.Warnings);
        Assert.Empty(outcome.Errors);
    }

    [Theory]
    [InlineData(CliExitCode.InvalidArguments, "invalid_arguments")]
    [InlineData(CliExitCode.FileError, "file_error")]
    [InlineData(CliExitCode.ValidationError, "validation_error")]
    [InlineData(CliExitCode.EnvironmentError, "environment_error")]
    [InlineData(CliExitCode.RuntimeError, "runtime_error")]
    [InlineData(CliExitCode.Cancelled, "cancelled")]
    public void FromCliResult_WhenFailed_ShouldMapTheExitCodeToAStableErrorCode(CliExitCode exitCode, string errorCode)
    {
        var outcome = McpToolOutcomeMapper.FromCliResult(
            CliCommandExecutionResult.Fail(exitCode, "Command failed.", errors: ["Detail."]));

        Assert.False(outcome.Success);
        Assert.Equal((int)exitCode, outcome.ExitCode);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal(errorCode, error.Code);
        Assert.Equal("Detail.", error.Message);
    }

    [Fact]
    public void FromCliResult_WhenFailedWithoutDetails_ShouldExposeTheTopLevelMessageAsAnError()
    {
        var outcome = McpToolOutcomeMapper.FromCliResult(
            CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Command failed."));

        var error = Assert.Single(outcome.Errors);
        Assert.Equal("runtime_error", error.Code);
        Assert.Equal("Command failed.", error.Message);
    }

    [Fact]
    public void JsonContext_ShouldSerializeTheStableOutcomeFieldNames()
    {
        var outcome = new McpToolOutcome(
            Success: false,
            ExitCode: (int)CliExitCode.ValidationError,
            Message: "Validation failed.",
            Warnings: ["A warning."],
            Errors: [new McpToolError("validation_error", "Detail.")]);

        var json = JsonSerializer.Serialize(outcome, McpJsonContext.Default.McpToolOutcome);

        Assert.Equal(
            "{\"success\":false,\"exitCode\":4,\"message\":\"Validation failed.\",\"warnings\":[\"A warning.\"],\"errors\":[{\"code\":\"validation_error\",\"message\":\"Detail.\"}]}",
            json);
    }

    [Fact]
    public void ErrorCodeCatalog_ShouldCoverEveryCliExitCode()
    {
        Assert.Equal(
            Enum.GetValues<CliExitCode>().Order(),
            McpCliErrorCodeCatalog.ByExitCode.Keys.Order());

        Assert.All(
            Enum.GetValues<CliExitCode>(),
            exitCode => Assert.False(string.IsNullOrWhiteSpace(McpCliErrorCodeCatalog.ByExitCode[exitCode])));
        Assert.Equal("runtime_error", McpCliErrorCodeCatalog.GetCode(999));
    }

    [Fact]
    public void FromException_ShouldMapCancellationWithoutExposingExceptionDetails()
    {
        var outcome = McpToolOutcomeMapper.FromException(new OperationCanceledException("secret cancellation detail"));

        Assert.False(outcome.Success);
        Assert.Equal((int)CliExitCode.Cancelled, outcome.ExitCode);
        Assert.Equal("cancelled", Assert.Single(outcome.Errors).Code);
        Assert.DoesNotContain("secret", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromException_ShouldMapUnexpectedFailuresWithoutExposingExceptionDetails()
    {
        var outcome = McpToolOutcomeMapper.FromException(new InvalidOperationException("secret backend detail"));

        Assert.False(outcome.Success);
        Assert.Equal((int)CliExitCode.RuntimeError, outcome.ExitCode);
        Assert.Equal("runtime_error", Assert.Single(outcome.Errors).Code);
        Assert.DoesNotContain("secret", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromCliResultRedactingErrorDetails_WhenFailed_ShouldRemoveWarningsAndDetails()
    {
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(
            CliCommandExecutionResult.Fail(
                CliExitCode.RuntimeError,
                "Command failed.",
                errors: ["backend-secret"],
                warnings: ["warning-secret"]));

        Assert.False(outcome.Success);
        Assert.Empty(outcome.Warnings);
        Assert.Equal("Command failed.", Assert.Single(outcome.Errors).Message);
    }

    [Theory]
    [InlineData("path_not_allowed")]
    [InlineData("approval_denied")]
    [InlineData("approval_timeout")]
    [InlineData("approval_unavailable")]
    [InlineData("tool_not_allowed")]
    public void SpecialFailures_ShouldKeepTheirStableErrorCodes(string expectedCode)
    {
        var outcome = expectedCode switch
        {
            "path_not_allowed" => McpToolOutcomeMapper.PathNotAllowed(),
            "approval_denied" => McpToolOutcomeMapper.ApprovalDenied(),
            "approval_timeout" => McpToolOutcomeMapper.ApprovalTimedOut(),
            "approval_unavailable" => McpToolOutcomeMapper.ApprovalUnavailable(),
            "tool_not_allowed" => McpToolOutcomeMapper.ToolNotAllowed(),
            _ => throw new InvalidOperationException(),
        };

        Assert.Equal(expectedCode, Assert.Single(outcome.Errors).Code);
    }
}
