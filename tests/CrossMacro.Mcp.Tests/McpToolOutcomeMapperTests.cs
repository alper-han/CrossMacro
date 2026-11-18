using System.Collections.Frozen;
using CrossMacro.Mcp.Serialization;

namespace CrossMacro.Mcp.Tests;

public sealed class McpToolOutcomeMapperTests
{
    [Theory]
    [InlineData(ScreenReadErrorKind.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    [InlineData(ScreenReadErrorKind.PermissionDenied, ExecutionOutcomeCode.EnvironmentError)]
    [InlineData(ScreenReadErrorKind.Canceled, ExecutionOutcomeCode.Cancelled)]
    [InlineData(ScreenReadErrorKind.CaptureTimeout, ExecutionOutcomeCode.RuntimeError)]
    public void FromScreenReadError_ShouldUseTheOutcomeCode(ScreenReadErrorKind kind, ExecutionOutcomeCode expected)
    {
        var outcome = McpToolOutcomeMapper.FromScreenReadError(kind, "failed");

        Assert.False(outcome.Success);
        Assert.Equal((int)expected, outcome.ExitCode);
    }

    [Theory]
    [InlineData(ScreenshotCaptureFailureKind.FileWriteFailed, null, ExecutionOutcomeCode.FileError)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.InvalidArguments, ExecutionOutcomeCode.InvalidArguments)]
    [InlineData(ScreenshotCaptureFailureKind.CaptureFailed, ScreenReadErrorKind.Canceled, ExecutionOutcomeCode.Cancelled)]
    [InlineData(ScreenshotCaptureFailureKind.ClipboardUnsupported, null, ExecutionOutcomeCode.EnvironmentError)]
    public void FromScreenshotCaptureFailure_ShouldUseTheOutcomeCode(
        ScreenshotCaptureFailureKind failureKind,
        ScreenReadErrorKind? readError,
        ExecutionOutcomeCode expected)
    {
        var outcome = McpToolOutcomeMapper.FromScreenshotCaptureFailure(failureKind, readError, "failed");

        Assert.Equal((int)expected, outcome.ExitCode);
    }

    [Fact]
    public void FromCliResult_WhenSuccessful_ShouldPreserveTheMessageAndWarnings()
    {
        var outcome = McpToolOutcomeMapper.FromResult(
            OperationResult.Ok("Completed.", warnings: ["A warning."]));

        Assert.True(outcome.Success);
        Assert.Equal((int)ExecutionOutcomeCode.Success, outcome.ExitCode);
        Assert.Equal("Completed.", outcome.Message);
        Assert.Equal(["A warning."], outcome.Warnings);
        Assert.Empty(outcome.Errors);
    }

    [Theory]
    [InlineData(ExecutionOutcomeCode.InvalidArguments, "invalid_arguments")]
    [InlineData(ExecutionOutcomeCode.FileError, "file_error")]
    [InlineData(ExecutionOutcomeCode.ValidationError, "validation_error")]
    [InlineData(ExecutionOutcomeCode.EnvironmentError, "environment_error")]
    [InlineData(ExecutionOutcomeCode.RuntimeError, "runtime_error")]
    [InlineData(ExecutionOutcomeCode.Cancelled, "cancelled")]
    public void FromCliResult_WhenFailed_ShouldMapTheExitCodeToAStableErrorCode(ExecutionOutcomeCode exitCode, string errorCode)
    {
        var outcome = McpToolOutcomeMapper.FromResult(
            OperationResult.Fail(exitCode, "Command failed.", errors: ["Detail."]));

        Assert.False(outcome.Success);
        Assert.Equal((int)exitCode, outcome.ExitCode);
        var error = Assert.Single(outcome.Errors);
        Assert.Equal(errorCode, error.Code);
        Assert.Equal("Detail.", error.Message);
    }

    [Fact]
    public void FromCliResult_WhenFailedWithoutDetails_ShouldExposeTheTopLevelMessageAsAnError()
    {
        var outcome = McpToolOutcomeMapper.FromResult(
            OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Command failed."));

        var error = Assert.Single(outcome.Errors);
        Assert.Equal("runtime_error", error.Code);
        Assert.Equal("Command failed.", error.Message);
    }

    [Fact]
    public void JsonContext_ShouldSerializeTheStableOutcomeFieldNames()
    {
        var outcome = new McpToolOutcome(
            Success: false,
            ExitCode: (int)ExecutionOutcomeCode.ValidationError,
            Message: "Validation failed.",
            Warnings: ["A warning."],
            Errors: [new McpToolError("validation_error", "Detail.")]);

        var json = JsonSerializer.Serialize(outcome, McpJsonContext.Default.McpToolOutcome);

        Assert.Equal(
            "{\"success\":false,\"exitCode\":4,\"message\":\"Validation failed.\",\"warnings\":[\"A warning.\"],\"errors\":[{\"code\":\"validation_error\",\"message\":\"Detail.\"}]}",
            json);
    }

    [Fact]
    public void ErrorCodeCatalog_ShouldCoverEveryOutcomeCode()
    {
        Assert.True(McpCliErrorCodeCatalog.ByExitCode is FrozenDictionary<ExecutionOutcomeCode, string>);
        Assert.Equal(
            Enum.GetValues<ExecutionOutcomeCode>().Order(),
            McpCliErrorCodeCatalog.ByExitCode.Keys.Order());

        Assert.All(
            Enum.GetValues<ExecutionOutcomeCode>(),
            exitCode => Assert.False(string.IsNullOrWhiteSpace(McpCliErrorCodeCatalog.ByExitCode[exitCode])));
        Assert.Equal("runtime_error", McpCliErrorCodeCatalog.GetCode(999));
    }

    [Fact]
    public void FromException_ShouldMapCancellationWithoutExposingExceptionDetails()
    {
        var outcome = McpToolOutcomeMapper.FromException(new OperationCanceledException("secret cancellation detail"));

        Assert.False(outcome.Success);
        Assert.Equal((int)ExecutionOutcomeCode.Cancelled, outcome.ExitCode);
        Assert.Equal("cancelled", Assert.Single(outcome.Errors).Code);
        Assert.DoesNotContain("secret", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromException_ShouldMapUnexpectedFailuresWithoutExposingExceptionDetails()
    {
        var outcome = McpToolOutcomeMapper.FromException(new InvalidOperationException("secret backend detail"));

        Assert.False(outcome.Success);
        Assert.Equal((int)ExecutionOutcomeCode.RuntimeError, outcome.ExitCode);
        Assert.Equal("runtime_error", Assert.Single(outcome.Errors).Code);
        Assert.DoesNotContain("secret", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromResultRedactingErrorDetails_WhenFailed_ShouldRemoveWarningsAndDetails()
    {
        var outcome = McpToolOutcomeMapper.FromResultRedactingErrorDetails(
            OperationResult.Fail(
                ExecutionOutcomeCode.RuntimeError,
                "Command failed.",
                errors: ["backend-secret"],
                warnings: ["warning-secret"]));

        Assert.False(outcome.Success);
        Assert.Empty(outcome.Warnings);
        Assert.Equal("Command failed.", Assert.Single(outcome.Errors).Message);
    }

    [Fact]
    public void FromResultRedactingErrorDetails_WhenSuccessful_ShouldPreserveWarnings()
    {
        var outcome = McpToolOutcomeMapper.FromResultRedactingErrorDetails(
            OperationResult.Ok("Completed.", warnings: ["warning"]));

        Assert.True(outcome.Success);
        Assert.Equal("Completed.", outcome.Message);
        Assert.Equal(["warning"], outcome.Warnings);
        Assert.Empty(outcome.Errors);
    }

    [Fact]
    public void FromMacroResult_ShouldMapWarningsAndErrorsToTheCommonEnvelope()
    {
        var outcome = McpToolOutcomeMapper.FromResult(new OperationResult
        {
            Success = false,
            Outcome = ExecutionOutcomeCode.ValidationError,
            Message = "Macro validation failed.",
            Warnings = ["warning"],
            Errors = ["step detail"],
        });

        Assert.False(outcome.Success);
        Assert.Equal((int)ExecutionOutcomeCode.ValidationError, outcome.ExitCode);
        Assert.Equal(["warning"], outcome.Warnings);
        Assert.Equal("validation_error", Assert.Single(outcome.Errors).Code);
        Assert.Equal("step detail", Assert.Single(outcome.Errors).Message);
    }

    [Fact]
    public void FromPreflightAndSettingsResults_ShouldPreservePublicMessagesAndRedactSettingsDetails()
    {
        var preflight = McpToolOutcomeMapper.FromPreflightResult(
            OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Preflight failed.", ["detail"], ["warning"]));
        var settings = McpToolOutcomeMapper.FromSettingsResult(new OperationResult
        {
            Success = false,
            Outcome = ExecutionOutcomeCode.RuntimeError,
            Message = "Settings operation failed.",
            Errors = ["secret detail"],
        });
        var unredactedSettings = McpToolOutcomeMapper.FromSettingsResult(
            new OperationResult
            {
                Success = false,
                Outcome = ExecutionOutcomeCode.RuntimeError,
                Message = "Settings operation failed.",
                Errors = ["diagnostic detail"],
            },
            redactDetails: false);

        Assert.Equal("invalid_arguments", Assert.Single(preflight.Errors).Code);
        Assert.Equal("Preflight failed.", Assert.Single(preflight.Errors).Message);
        Assert.Equal("Settings operation failed.", Assert.Single(settings.Errors).Message);
        Assert.Equal("diagnostic detail", Assert.Single(unredactedSettings.Errors).Message);
    }

    [Theory]
    [InlineData("path_not_allowed")]
    [InlineData("tool_not_allowed")]
    public void SpecialFailures_ShouldKeepTheirStableErrorCodes(string expectedCode)
    {
        var outcome = expectedCode switch
        {
            "path_not_allowed" => McpToolOutcomeMapper.PathNotAllowed(),
            "tool_not_allowed" => McpToolOutcomeMapper.ToolNotAllowed(),
            _ => throw new InvalidOperationException(),
        };

        Assert.Equal(expectedCode, Assert.Single(outcome.Errors).Code);
    }
}
