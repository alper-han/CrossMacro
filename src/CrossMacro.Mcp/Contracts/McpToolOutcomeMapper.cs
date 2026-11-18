
namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Maps application operation results to the MCP v1 common result envelope without
/// routing formatted CLI output through the MCP protocol stream.
/// </summary>
public static class McpToolOutcomeMapper
{
    public static McpToolOutcome Success(string message)
    {
        return new McpToolOutcome(
            Success: true,
            ExitCode: (int)ExecutionOutcomeCode.Success,
            Message: message,
            Warnings: [],
            Errors: []);
    }

    public static McpToolOutcome InvalidArguments(string message, string? detail = null)
    {
        return Error(ExecutionOutcomeCode.InvalidArguments, message, detail);
    }

    public static McpToolOutcome Denied(string message, string? detail = null)
    {
        return new McpToolOutcome(
            Success: false,
            ExitCode: (int)ExecutionOutcomeCode.EnvironmentError,
            Message: message,
            Warnings: [],
            Errors: [new McpToolError("capability_denied", string.IsNullOrWhiteSpace(detail) ? message : detail)]);
    }

    public static McpToolOutcome PathNotAllowed() =>
        Error(ExecutionOutcomeCode.EnvironmentError, "The requested MCP path is not allowed.", "The requested path is outside the configured MCP roots.", "path_not_allowed");

    public static McpToolOutcome PathRootNotConfigured(string settingKey) =>
        Error(ExecutionOutcomeCode.EnvironmentError, "No MCP path root is configured for this kind of path.", $"No MCP path root is configured for this kind of path. Set {settingKey} to one or more absolute directories to allow access.", "path_not_allowed");

    public static McpToolOutcome ToolNotAllowed() =>
        Error(ExecutionOutcomeCode.EnvironmentError, "The requested MCP tool is not registered.", "Only tools in the CrossMacro MCP catalog are available.", "tool_not_allowed");

    public static McpToolOutcome FileError(string message, string? detail = null)
    {
        return Error(ExecutionOutcomeCode.FileError, message, detail);
    }

    public static McpToolOutcome RuntimeError(string message)
    {
        return Error(ExecutionOutcomeCode.RuntimeError, message, detail: null);
    }

    public static McpToolOutcome EnvironmentError(string message)
    {
        return Error(ExecutionOutcomeCode.EnvironmentError, message, detail: null);
    }

    public static McpToolOutcome Cancelled(string message)
    {
        return Error(ExecutionOutcomeCode.Cancelled, message, detail: null);
    }

    public static McpToolOutcome ValidationError(string message)
    {
        return Error(ExecutionOutcomeCode.ValidationError, message, detail: null);
    }

    public static McpToolOutcome FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is OperationCanceledException
            ? Cancelled("The CrossMacro command was cancelled.")
            : RuntimeError("The CrossMacro command could not be completed.");
    }

    public static McpToolOutcome FromScreenReadError(ScreenReadErrorKind? errorKind, string message) =>
        Error(ScreenFailureOutcomes.ForReadError(errorKind), message, detail: null);

    public static McpToolOutcome FromScreenshotCaptureFailure(
        ScreenshotCaptureFailureKind failureKind,
        ScreenReadErrorKind? screenReadErrorKind,
        string message) =>
        Error(ScreenFailureOutcomes.ForScreenshot(failureKind, screenReadErrorKind), message, detail: null);

    public static McpToolOutcome FromResult(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var warnings = result.Warnings.ToArray();
        if (result.Success)
        {
            return new McpToolOutcome(
                Success: true,
                ExitCode: (int)result.Outcome,
                Message: result.Message,
                Warnings: warnings,
                Errors: []);
        }

        var code = ToErrorCode((int)result.Outcome);
        var errors = result.Errors.Count is 0
            ? [new McpToolError(code, result.Message)]
            : result.Errors.Select(message => new McpToolError(code, message)).ToArray();

        return new McpToolOutcome(
            Success: false,
            ExitCode: (int)result.Outcome,
            Message: result.Message,
            Warnings: warnings,
            Errors: errors);
    }

    public static McpToolOutcome FromResultRedactingErrorDetails(OperationResult result)
    {
        var outcome = FromResult(result);
        if (outcome.Success)
        {
            return outcome;
        }

        return new McpToolOutcome(
            Success: false,
            ExitCode: outcome.ExitCode,
            Message: outcome.Message,
            Warnings: [],
            Errors: [new McpToolError(outcome.Errors[0].Code, outcome.Message)]);
    }

    public static McpToolOutcome FromPreflightResult(OperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Success
            ? Success("Preflight check passed.")
            : Error(result.Outcome, result.Message, detail: null);
    }

    public static McpToolOutcome FromSettingsResult(OperationResult result, bool redactDetails = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Success)
        {
            return new McpToolOutcome(
                Success: true,
                ExitCode: (int)result.Outcome,
                Message: result.Message,
                Warnings: [],
                Errors: []);
        }

        var code = ToErrorCode((int)result.Outcome);
        var message = redactDetails || result.Errors.Count is 0
            ? result.Message
            : result.Errors[0];
        return Error(result.Outcome, result.Message, message, code);
    }

    private static string ToErrorCode(int exitCode) => McpCliErrorCodeCatalog.GetCode(exitCode);

    private static McpToolOutcome Error(ExecutionOutcomeCode exitCode, string message, string? detail, string? errorCode = null)
    {
        var errorMessage = string.IsNullOrWhiteSpace(detail) ? message : detail;
        return new McpToolOutcome(
            Success: false,
            ExitCode: (int)exitCode,
            Message: message,
            Warnings: [],
            Errors: [new McpToolError(errorCode ?? ToErrorCode((int)exitCode), errorMessage)]);
    }
}
