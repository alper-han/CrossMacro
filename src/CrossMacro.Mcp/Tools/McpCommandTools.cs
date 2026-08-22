namespace CrossMacro.Mcp.Tools;

public sealed class McpCommandTools(
    IMacroExecutionService macroExecutionService,
    IMcpOperationCoordinator operationCoordinator,
    IRunScriptExecutionService runScriptExecutionService,
    IRecordExecutionService recordExecutionService,
    ICliPreflightService cliPreflightService,
    CliCommandExecutor cliCommandExecutor,
    IMcpCommandPolicy commandPolicy,
    McpToolAuthorization authorization,
    McpPathAuthorizer pathAuthorizer)
{
    private const int MaximumAutomationTimeoutSeconds = 3_600;
    private const int DefaultAutomationTimeoutSeconds = MaximumAutomationTimeoutSeconds;

    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;
    private readonly IMcpOperationCoordinator _operationCoordinator = operationCoordinator;
    private readonly IRunScriptExecutionService _runScriptExecutionService = runScriptExecutionService;
    private readonly IRecordExecutionService _recordExecutionService = recordExecutionService;
    private readonly ICliPreflightService _cliPreflightService = cliPreflightService;
    private readonly CliCommandExecutor _cliCommandExecutor = cliCommandExecutor;
    private readonly IMcpCommandPolicy _commandPolicy = commandPolicy;
    private readonly McpToolAuthorization _authorization = authorization;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;

    [McpServerTool(Name = "command.execute", Title = "Execute a CrossMacro command", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpCommandExecuteResult))]
    [Description("Executes a restricted existing CrossMacro CLI command using a command token and argument array, never a shell command string.")]
    public async Task<CallToolResult> ExecuteCommandAsync(
        string command,
        IReadOnlyList<string>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var capability = _authorization.Require(McpCapability.CommandExecute);
        if (capability is not null)
        {
            return CreateCommandExecuteToolResult(capability, command.Trim(), operationStarted: false, operationId: null);
        }

        var commandArguments = arguments ?? [];
        var policyOutcome = _commandPolicy.Validate(command, commandArguments);
        if (!policyOutcome.Success)
        {
            return CreateCommandExecuteToolResult(policyOutcome, command.Trim(), operationStarted: false, operationId: null);
        }

        var normalizedCommand = command.Trim().ToLowerInvariant();
        var parseArguments = new string[commandArguments.Count + 1];
        parseArguments[0] = normalizedCommand;
        for (var index = 0; index < commandArguments.Count; index++)
        {
            parseArguments[index + 1] = commandArguments[index];
        }

        var parseResult = CliCommandRouter.Parse(parseArguments);
        if (parseResult.Kind is not CliParseResult.ParseResultKind.Success || parseResult.Options is null)
        {
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.InvalidArguments(parseResult.ErrorMessage ?? "Command arguments are invalid."),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        if (parseResult.Options is McpCliOptions or HeadlessCliOptions or QuickSetupCliOptions)
        {
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.InvalidArguments("This command is not available through command.execute."),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        var commandCapability = _authorization.RequireCommand(parseResult.Options);
        if (commandCapability is not null)
        {
            return CreateCommandExecuteToolResult(
                commandCapability,
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        if (!_authorization.TryAuthorizeCommandOptions(parseResult.Options, out var authorizedOptions, out var authorizationError))
        {
            return CreateCommandExecuteToolResult(
                authorizationError,
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        var taskAuthorization = await _authorization.TryAuthorizeParsedCommandTaskMacroAsync(authorizedOptions, cancellationToken).ConfigureAwait(false);
        if (taskAuthorization is not null)
        {
            return CreateCommandExecuteToolResult(
                taskAuthorization,
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        if (authorizedOptions is PlayCliOptions playOptions)
        {
            return await StartParsedPlayCommandAsync(playOptions, normalizedCommand, cancellationToken).ConfigureAwait(false);
        }

        if (authorizedOptions is RunCliOptions runOptions)
        {
            return await StartParsedRunCommandAsync(runOptions, normalizedCommand, cancellationToken).ConfigureAwait(false);
        }

        if (authorizedOptions is RecordCliOptions recordOptions)
        {
            return await StartParsedRecordCommandAsync(recordOptions, normalizedCommand, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var result = await _cliCommandExecutor.ExecuteResultAsync(authorizedOptions, cancellationToken).ConfigureAwait(false);
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.FromException(exception),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }
    }

    private async Task<CallToolResult> StartParsedPlayCommandAsync(PlayCliOptions options, string command, CancellationToken cancellationToken)
    {
        var macroReadCapability = _authorization.Require(McpCapability.MacroRead);
        if (macroReadCapability is not null)
        {
            return CreateCommandExecuteToolResult(macroReadCapability, command, operationStarted: false, operationId: null);
        }

        if (!_pathAuthorizer.TryNormalizeMacroPath(options.MacroFilePath, out var normalizedMacroPath, out var pathError))
        {
            return CreateCommandExecuteToolResult(pathError, command, operationStarted: false, operationId: null);
        }

        options = options with
        {
            MacroFilePath = normalizedMacroPath,
            TimeoutSeconds = GetMcpAutomationTimeoutSeconds(options.TimeoutSeconds),
        };
        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Play, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateCommandExecuteToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), command, operationStarted: false, operationId: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Play,
            token => ExecuteParsedPlayAsync(options, token),
            CancellationToken.None);
        return CreateCommandExecuteToolResult(
            start.Error ?? McpToolOutcomeMapper.Success("Command operation started."),
            command,
            operationStarted: start.Started,
            operationId: start.Operation?.OperationId);
    }

    private async Task<CallToolResult> StartParsedRunCommandAsync(RunCliOptions options, string command, CancellationToken cancellationToken)
    {
        var normalizedStepFilePath = options.StepFilePath;
        if (options.StepFilePath is not null)
        {
            var fileReadCapability = _authorization.Require(McpCapability.FileRead);
            if (fileReadCapability is not null)
            {
                return CreateCommandExecuteToolResult(fileReadCapability, command, operationStarted: false, operationId: null);
            }

            if (!_pathAuthorizer.TryAuthorizeFileReadPath(options.StepFilePath, out normalizedStepFilePath, out var stepPathError))
            {
                return CreateCommandExecuteToolResult(stepPathError, command, operationStarted: false, operationId: null);
            }
        }

        if (options.StepFilePath is not null)
        {
            options = options with { StepFilePath = normalizedStepFilePath };

            var shellCapability = _authorization.Require(McpCapability.ShellExecute);
            if (shellCapability is not null)
            {
                return CreateCommandExecuteToolResult(shellCapability, command, operationStarted: false, operationId: null);
            }
        }

        options = options with { TimeoutSeconds = GetMcpAutomationTimeoutSeconds(options.TimeoutSeconds) };

        var inlineShellCapability = _authorization.RequireShell(options.Steps);
        if (inlineShellCapability is not null)
        {
            return CreateCommandExecuteToolResult(inlineShellCapability, command, operationStarted: false, operationId: null);
        }

        if (options.ImageAssets is not null)
        {
            var fileReadCapability = _authorization.Require(McpCapability.FileRead);
            if (fileReadCapability is not null)
            {
                return CreateCommandExecuteToolResult(fileReadCapability, command, operationStarted: false, operationId: null);
            }

            var normalizedAssets = new List<RunImageAssetCliOption>(options.ImageAssets.Count);
            foreach (var asset in options.ImageAssets)
            {
                if (!_pathAuthorizer.TryAuthorizeImageOrMacroReadPath(asset.FilePath, out var normalizedAssetPath, out var assetPathError))
                {
                    return CreateCommandExecuteToolResult(assetPathError, command, operationStarted: false, operationId: null);
                }

                normalizedAssets.Add(asset with { FilePath = normalizedAssetPath });
            }

            options = options with { ImageAssets = normalizedAssets };
        }

        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Run, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateCommandExecuteToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), command, operationStarted: false, operationId: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Run,
            token => ExecuteParsedRunAsync(options, token),
            CancellationToken.None);
        return CreateCommandExecuteToolResult(
            start.Error ?? McpToolOutcomeMapper.Success("Command operation started."),
            command,
            operationStarted: start.Started,
            operationId: start.Operation?.OperationId);
    }

    private async Task<CallToolResult> StartParsedRecordCommandAsync(RecordCliOptions options, string command, CancellationToken cancellationToken)
    {
        var fileWriteCapability = _authorization.Require(McpCapability.FileWrite);
        if (fileWriteCapability is not null)
        {
            return CreateCommandExecuteToolResult(fileWriteCapability, command, operationStarted: false, operationId: null);
        }

        if (!_pathAuthorizer.TryNormalizeRecordingOutputPath(options.OutputFilePath, out var normalizedOutputPath, out var pathError))
        {
            return CreateCommandExecuteToolResult(pathError, command, operationStarted: false, operationId: null);
        }

        options = options with
        {
            OutputFilePath = normalizedOutputPath,
            DurationSeconds = GetMcpAutomationTimeoutSeconds(options.DurationSeconds),
        };
        var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Record, cancellationToken).ConfigureAwait(false);
        if (!preflight.Success)
        {
            return CreateCommandExecuteToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), command, operationStarted: false, operationId: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Record,
            token => ExecuteParsedRecordAsync(options, token),
            CancellationToken.None);
        return CreateCommandExecuteToolResult(
            start.Error ?? McpToolOutcomeMapper.Success("Command operation started."),
            command,
            operationStarted: start.Started,
            operationId: start.Operation?.OperationId);
    }

    private async Task<CliCommandExecutionResult> ExecuteParsedPlayAsync(PlayCliOptions options, CancellationToken cancellationToken)
    {
        var result = await RunWithTimeoutAsync(
            options.TimeoutSeconds,
            token => _macroExecutionService.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = options.MacroFilePath,
                SpeedMultiplier = options.SpeedMultiplier,
                Loop = options.Loop || options.RepeatCount is not 1,
                RepeatCount = options.RepeatCount,
                RepeatDelayMs = options.RepeatDelayMs,
                MotionMode = options.MotionMode,
                StrictSpeedMotionEventsPerSecond = options.StrictSpeedMotionEventsPerSecond,
                PrecisionMotionEventsPerSecond = options.PrecisionMotionEventsPerSecond,
                MaximumMotionErrorPixels = options.MaximumMotionErrorPixels,
                CountdownSeconds = options.CountdownSeconds,
                DryRun = options.DryRun,
            }, token),
            cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteParsedRunAsync(RunCliOptions options, CancellationToken cancellationToken)
    {
        var result = await RunWithTimeoutAsync(
            options.TimeoutSeconds,
            token => _runScriptExecutionService.ExecuteAsync(new RunCliExecutionRequest
            {
                Steps = options.Steps,
                StepFilePath = options.StepFilePath,
                SpeedMultiplier = options.SpeedMultiplier,
                CountdownSeconds = options.CountdownSeconds,
                DryRun = options.DryRun,
                ImageAssets = options.ImageAssets ?? [],
            }, token),
            cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteParsedRecordAsync(RecordCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _recordExecutionService.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = options.OutputFilePath,
            RecordMouse = options.RecordMouse,
            RecordKeyboard = options.RecordKeyboard,
            CoordinateMode = options.CoordinateMode,
            SkipInitialZero = options.SkipInitialZero,
            DurationSeconds = options.DurationSeconds,
        }, cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private static async Task<MacroExecutionResult> RunWithTimeoutAsync(
        int timeoutSeconds,
        Func<CancellationToken, Task<MacroExecutionResult>> executeAsync,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeoutSeconds, 0);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var result = await executeAsync(timeout.Token).ConfigureAwait(false);
            return timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                ? TimedOutResult()
                : result;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return TimedOutResult();
        }
    }

    private static CliCommandExecutionResult ToCliResult(MacroExecutionResult result) =>
        result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);

    private static CliCommandExecutionResult ToCliResult(RecordExecutionResult result) =>
        result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);

    private static MacroExecutionResult TimedOutResult() => new()
    {
        Success = false,
        ExitCode = CliExitCode.RuntimeError,
        Message = "Automation operation timed out.",
    };

    private static int GetMcpAutomationTimeoutSeconds(int timeoutSeconds) =>
        timeoutSeconds is > 0 and <= MaximumAutomationTimeoutSeconds
            ? timeoutSeconds
            : DefaultAutomationTimeoutSeconds;

    private static CallToolResult CreateCommandExecuteToolResult(
        McpToolOutcome outcome,
        string command,
        bool operationStarted,
        string? operationId) =>
        new()
        {
            Content = [new TextContentBlock { Text = outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(
                new McpCommandExecuteResult(outcome, command, operationStarted, operationId),
                McpJsonContext.Default.McpCommandExecuteResult),
            IsError = !outcome.Success,
        };
}
