using CrossMacro.Cli;
using CrossMacro.Cli.Services;

namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Maps existing CLI outcomes to the MCP v1 common result envelope without
/// routing formatted CLI output through the MCP protocol stream.
/// </summary>
public static class McpToolOutcomeMapper
{
    public static McpToolOutcome Success(string message)
    {
        return new McpToolOutcome(
            Success: true,
            ExitCode: (int)CliExitCode.Success,
            Message: message,
            Warnings: [],
            Errors: []);
    }

    public static McpToolOutcome InvalidArguments(string message, string? detail = null)
    {
        return Error(CliExitCode.InvalidArguments, message, detail);
    }

    public static McpToolOutcome Denied(string message, string? detail = null)
    {
        return new McpToolOutcome(
            Success: false,
            ExitCode: (int)CliExitCode.EnvironmentError,
            Message: message,
            Warnings: [],
            Errors: [new McpToolError("capability_denied", string.IsNullOrWhiteSpace(detail) ? message : detail)]);
    }

    public static McpToolOutcome PathNotAllowed() =>
        Error(CliExitCode.EnvironmentError, "The requested MCP path is not allowed.", "The requested path is outside the configured MCP roots.", "path_not_allowed");

    public static McpToolOutcome ApprovalDenied() =>
        Error(CliExitCode.EnvironmentError, "CrossMacro approval was denied.", "The effectful MCP operation was not approved by CrossMacro.", "approval_denied");

    public static McpToolOutcome ApprovalTimedOut() =>
        Error(CliExitCode.EnvironmentError, "CrossMacro approval timed out.", "The effectful MCP operation was not approved before the timeout.", "approval_timeout");

    public static McpToolOutcome ApprovalUnavailable() =>
        Error(CliExitCode.EnvironmentError, "CrossMacro approval is unavailable.", "The effectful MCP operation could not be sent to the approval service.", "approval_unavailable");

    public static McpToolOutcome ToolNotAllowed() =>
        Error(CliExitCode.EnvironmentError, "The requested MCP tool is not registered.", "Only tools in the CrossMacro MCP catalog are available.", "tool_not_allowed");

    public static McpToolOutcome FileError(string message, string? detail = null)
    {
        return Error(CliExitCode.FileError, message, detail);
    }

    public static McpToolOutcome RuntimeError(string message)
    {
        return Error(CliExitCode.RuntimeError, message, detail: null);
    }

    public static McpToolOutcome EnvironmentError(string message)
    {
        return Error(CliExitCode.EnvironmentError, message, detail: null);
    }

    public static McpToolOutcome Cancelled(string message)
    {
        return Error(CliExitCode.Cancelled, message, detail: null);
    }

    public static McpToolOutcome ValidationError(string message)
    {
        return Error(CliExitCode.ValidationError, message, detail: null);
    }

    public static McpToolOutcome FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is OperationCanceledException
            ? Cancelled("The CrossMacro command was cancelled.")
            : RuntimeError("The CrossMacro command could not be completed.");
    }

    public static McpToolOutcome FromScreenshotCaptureFailure(
        ScreenshotCaptureFailureKind failureKind,
        ScreenReadErrorKind? screenReadErrorKind,
        string message)
    {
        var exitCode = CliExitCode.RuntimeError;
        if (failureKind is ScreenshotCaptureFailureKind.ProviderUnsupported or ScreenshotCaptureFailureKind.ClipboardUnsupported)
        {
            exitCode = CliExitCode.EnvironmentError;
        }
        else if (failureKind is ScreenshotCaptureFailureKind.FileWriteFailed)
        {
            exitCode = CliExitCode.FileError;
        }
        else if (failureKind is ScreenshotCaptureFailureKind.CaptureFailed)
        {
            if (screenReadErrorKind is ScreenReadErrorKind.InvalidArguments)
            {
                exitCode = CliExitCode.InvalidArguments;
            }
            else if (screenReadErrorKind is ScreenReadErrorKind.Unsupported
                or ScreenReadErrorKind.PermissionDenied
                or ScreenReadErrorKind.BackendUnavailable)
            {
                exitCode = CliExitCode.EnvironmentError;
            }
        }
        return Error(exitCode, message, detail: null);
    }

    public static McpToolOutcome FromCliResult(CliCommandExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var warnings = result.Warnings.ToArray();
        if (result.Success)
        {
            return new McpToolOutcome(
                Success: true,
                ExitCode: result.ExitCode,
                Message: result.Message,
                Warnings: warnings,
                Errors: []);
        }

        var code = ToErrorCode(result.ExitCode);
        var errors = result.Errors.Count is 0
            ? [new McpToolError(code, result.Message)]
            : result.Errors.Select(message => new McpToolError(code, message)).ToArray();

        return new McpToolOutcome(
            Success: false,
            ExitCode: result.ExitCode,
            Message: result.Message,
            Warnings: warnings,
            Errors: errors);
    }

    public static McpToolOutcome FromMacroResult(MacroExecutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var warnings = result.Warnings.ToArray();
        if (result.Success)
        {
            return new McpToolOutcome(
                Success: true,
                ExitCode: (int)result.ExitCode,
                Message: result.Message,
                Warnings: warnings,
                Errors: []);
        }

        var code = ToErrorCode((int)result.ExitCode);
        var errors = result.Errors.Count is 0
            ? [new McpToolError(code, result.Message)]
            : result.Errors.Select(message => new McpToolError(code, message)).ToArray();

        return new McpToolOutcome(
            Success: false,
            ExitCode: (int)result.ExitCode,
            Message: result.Message,
            Warnings: warnings,
            Errors: errors);
    }

    public static McpToolOutcome FromCliResultRedactingErrorDetails(CliCommandExecutionResult result)
    {
        var outcome = FromCliResult(result);
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

    public static McpToolOutcome FromPreflightResult(CliPreflightResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Success
            ? Success("Preflight check passed.")
            : Error(result.ExitCode, result.Message, detail: null);
    }

    public static McpToolOutcome FromSettingsResult(SettingsCommandResult result, bool redactDetails = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Success)
        {
            return new McpToolOutcome(
                Success: true,
                ExitCode: (int)result.ExitCode,
                Message: result.Message,
                Warnings: [],
                Errors: []);
        }

        var code = ToErrorCode((int)result.ExitCode);
        var message = redactDetails || result.Errors.Count is 0
            ? result.Message
            : result.Errors[0];
        return Error(result.ExitCode, result.Message, message, code);
    }

    private static string ToErrorCode(int exitCode) => McpCliErrorCodeCatalog.GetCode(exitCode);

    private static McpToolOutcome Error(CliExitCode exitCode, string message, string? detail, string? errorCode = null)
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
