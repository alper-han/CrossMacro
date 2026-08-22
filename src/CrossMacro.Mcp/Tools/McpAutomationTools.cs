namespace CrossMacro.Mcp.Tools;

public sealed class McpAutomationTools(
    IMacroExecutionService macroExecutionService,
    IMcpOperationCoordinator operationCoordinator,
    IRunScriptExecutionService runScriptExecutionService,
    IRecordExecutionService recordExecutionService,
    ICliPreflightService cliPreflightService,
    McpToolAuthorization authorization,
    McpPathAuthorizer pathAuthorizer)
{
    private const int MaximumAutomationTimeoutSeconds = 3_600;
    private const int DefaultAutomationTimeoutSeconds = MaximumAutomationTimeoutSeconds;
    private const int MaximumAutomationRepeatDelayMs = 3_600_000;
    private const int MaximumAutomationRecordDurationSeconds = 3_600;
    private const int MaximumAutomationStepCount = 100;
    private const int MaximumAutomationStepCharacters = 16_384;
    private const int MaximumAutomationStepPayloadCharacters = 262_144;

    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;
    private readonly IMcpOperationCoordinator _operationCoordinator = operationCoordinator;
    private readonly IRunScriptExecutionService _runScriptExecutionService = runScriptExecutionService;
    private readonly IRecordExecutionService _recordExecutionService = recordExecutionService;
    private readonly ICliPreflightService _cliPreflightService = cliPreflightService;
    private readonly McpToolAuthorization _authorization = authorization;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;

    [McpServerTool(Name = "automation.start", Title = "Start automation", ReadOnly = false, Destructive = false, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpAutomationStartResult))]
    [Description("Starts one bounded play, run, or record operation and returns an opaque operation ID. Use automation.get and automation.stop for its lifecycle.")]
    public async Task<CallToolResult> StartAutomationAsync(string kind, string? macroPath = null, IReadOnlyList<string>? steps = null, string? outputPath = null, string? stepFilePath = null, IReadOnlyList<McpRunImageAsset>? imageAssets = null, double? speedMultiplier = null, bool loop = false, int? repeatCount = null, int? repeatDelayMs = null, int? countdownSeconds = null, int? timeoutSeconds = null, int? durationSeconds = null, bool dryRun = false, bool? recordMouse = null, bool? recordKeyboard = null, string? coordinateMode = null, string? motionMode = null, int? strictSpeedMotionEventsPerSecond = null, int? precisionMotionEventsPerSecond = null, double? maximumMotionErrorPixels = null, bool skipInitialZero = false, CancellationToken cancellationToken = default)
    {
        string? normalizedKind = kind?.Trim().ToLowerInvariant();
        McpToolOutcome? capability = _authorization.RequireAutomation(normalizedKind);
        if (capability is not null)
        {
            return CreateStartResult(capability, operation: null);
        }

        var request = new AutomationRequest(macroPath, steps, outputPath, stepFilePath, imageAssets, speedMultiplier, loop, repeatCount, repeatDelayMs, countdownSeconds, timeoutSeconds, durationSeconds, dryRun, recordMouse, recordKeyboard, coordinateMode, motionMode, strictSpeedMotionEventsPerSecond, precisionMotionEventsPerSecond, maximumMotionErrorPixels, skipInitialZero);
        return normalizedKind switch
        {
            "play" => await StartPlayAsync(request, cancellationToken).ConfigureAwait(false),
            "run" => await StartRunAsync(request, cancellationToken).ConfigureAwait(false),
            "record" => await StartRecordAsync(request, cancellationToken).ConfigureAwait(false),
            _ => CreateStartResult(McpToolOutcomeMapper.InvalidArguments("Automation kind must be play, run, or record."), operation: null),
        };
    }

    [McpServerTool(Name = "automation.get", Title = "Get automation status", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpAutomationGetResult))]
    [Description("Gets an automation operation state and its final redacted outcome without returning original arguments or execution data.")]
    public CallToolResult GetAutomation(string operationId)
    {
        McpToolOutcome? capability = _authorization.Require(McpCapability.StatusRead);
        if (capability is not null)
        {
            return CreateGetResult(capability, operation: null);
        }

        operationId ??= string.Empty;
        if (!IsValidOperationId(operationId))
        {
            return CreateGetResult(McpToolOutcomeMapper.InvalidArguments("Automation operation ID is invalid."), operation: null);
        }

        McpAutomationOperation? operation = _operationCoordinator.GetOperation(operationId);
        return operation is null
            ? CreateGetResult(McpToolOutcomeMapper.InvalidArguments("Automation operation was not found."), operation: null)
            : CreateGetResult(McpToolOutcomeMapper.Success("Automation operation status retrieved."), operation);
    }

    [McpServerTool(Name = "automation.stop", Title = "Stop automation", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpAutomationStopResult))]
    [Description("Requests cancellation for an automation operation. Repeated requests are safe and report whether cancellation was newly initiated.")]
    public CallToolResult StopAutomation(string operationId)
    {
        if (!_authorization.IsAnyAllowed(McpCapability.InputAutomation, McpCapability.Recording, McpCapability.CommandExecute))
        {
            return CreateStopResult(_authorization.Require(McpCapability.InputAutomation)!, operation: null, cancellationInitiated: false);
        }

        operationId ??= string.Empty;
        if (!IsValidOperationId(operationId))
        {
            return CreateStopResult(McpToolOutcomeMapper.InvalidArguments("Automation operation ID is invalid."), operation: null, cancellationInitiated: false);
        }

        McpAutomationOperationStopResult result = _operationCoordinator.StopOperation(operationId);
        if (!result.Found)
        {
            return CreateStopResult(McpToolOutcomeMapper.InvalidArguments("Automation operation was not found."), operation: null, cancellationInitiated: false);
        }

        McpToolOutcome outcome = result.CancellationInitiated
            ? McpToolOutcomeMapper.Success("Automation cancellation requested.")
            : McpToolOutcomeMapper.Success("Automation operation is already completed or cancellation was already requested.");
        return CreateStopResult(outcome, result.Operation, result.CancellationInitiated);
    }

    private async Task<CallToolResult> StartPlayAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        if (request.Steps is not null || request.OutputPath is not null || request.StepFilePath is not null || request.ImageAssets is not null || request.DurationSeconds is not null || request.RecordMouse is not null || request.RecordKeyboard is not null || request.CoordinateMode is not null || request.SkipInitialZero)
        {
            return CreateStartResult(McpToolOutcomeMapper.InvalidArguments("Play automation accepts macroPath and playback options only."), operation: null);
        }

        McpToolOutcome? macroReadCapability = _authorization.Require(McpCapability.MacroRead);
        if (macroReadCapability is not null)
        {
            return CreateStartResult(macroReadCapability, operation: null);
        }

        if (!_pathAuthorizer.TryNormalizeMacroPath(request.MacroPath ?? string.Empty, out string? macroPath, out McpToolOutcome? pathError))
        {
            return CreateStartResult(pathError, operation: null);
        }

        if (!TryGetPlaybackOptions(request, out AutomationPlaybackOptions? options, out McpToolOutcome? optionsError))
        {
            return CreateStartResult(optionsError, operation: null);
        }

        if (!request.DryRun)
        {
            CliPreflightResult preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Play, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateStartResult(McpToolOutcomeMapper.FromPreflightResult(preflight), operation: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        McpAutomationOperationStartResult start = _operationCoordinator.Start(McpAutomationOperationKind.Play, token => ExecutePlayAsync(macroPath, options, request.DryRun, token), CancellationToken.None);
        return CreateStartResult(start.Error ?? McpToolOutcomeMapper.Success("Automation operation started."), start.Operation);
    }

    private async Task<CallToolResult> StartRunAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        if (request.MacroPath is not null || request.OutputPath is not null || request.MotionMode is not null || request.StrictSpeedMotionEventsPerSecond is not null || request.PrecisionMotionEventsPerSecond is not null || request.MaximumMotionErrorPixels is not null || request.Loop || request.RepeatCount is not null || request.RepeatDelayMs is not null || request.DurationSeconds is not null || request.RecordMouse is not null || request.RecordKeyboard is not null || request.CoordinateMode is not null || request.SkipInitialZero)
        {
            return CreateStartResult(McpToolOutcomeMapper.InvalidArguments("Run automation accepts steps and run options only."), operation: null);
        }

        McpToolOutcome? commandExecutionCapability = _authorization.Require(McpCapability.CommandExecute);
        if (commandExecutionCapability is not null)
        {
            return CreateStartResult(commandExecutionCapability, operation: null);
        }

        if (!TryGetRunOptions(request, out RunOptions? options, out McpToolOutcome? optionsError))
        {
            return CreateStartResult(optionsError, operation: null);
        }

        IReadOnlyList<string> steps = [];
        if (request.Steps is not null && !TryValidateSteps(request.Steps, out steps, out McpToolOutcome? stepsError))
        {
            return CreateStartResult(stepsError, operation: null);
        }

        if (steps.Count is 0 && string.IsNullOrWhiteSpace(request.StepFilePath))
        {
            return CreateStartResult(McpToolOutcomeMapper.InvalidArguments("Run automation requires steps or stepFilePath."), operation: null);
        }

        McpToolOutcome? shellCapability = _authorization.RequireShell(steps);
        if (shellCapability is not null)
        {
            return CreateStartResult(shellCapability, operation: null);
        }

        string? stepFilePath = request.StepFilePath;
        if (stepFilePath is not null)
        {
            McpToolOutcome? fileReadCapability = _authorization.Require(McpCapability.FileRead);
            if (fileReadCapability is not null)
            {
                return CreateStartResult(fileReadCapability, operation: null);
            }

            if (!_pathAuthorizer.TryAuthorizeFileReadPath(stepFilePath, out stepFilePath, out McpToolOutcome? stepPathError))
            {
                return CreateStartResult(stepPathError, operation: null);
            }

            McpToolOutcome? stepFileShellCapability = _authorization.Require(McpCapability.ShellExecute);
            if (stepFileShellCapability is not null)
            {
                return CreateStartResult(stepFileShellCapability, operation: null);
            }
        }

        AssetNormalizationResult assets = NormalizeImageAssets(request.ImageAssets);
        if (!assets.Success)
        {
            return CreateStartResult(assets.Error!, operation: null);
        }

        if (!request.DryRun)
        {
            CliPreflightResult preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Run, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateStartResult(McpToolOutcomeMapper.FromPreflightResult(preflight), operation: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        McpAutomationOperationStartResult start = _operationCoordinator.Start(McpAutomationOperationKind.Run, token => ExecuteRunAsync(steps, stepFilePath, assets.Assets, options, request.DryRun, token), CancellationToken.None);
        return CreateStartResult(start.Error ?? McpToolOutcomeMapper.Success("Automation operation started."), start.Operation);
    }

    private async Task<CallToolResult> StartRecordAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        if (request.MacroPath is not null || request.Steps is not null || request.StepFilePath is not null || request.ImageAssets is not null || request.SpeedMultiplier is not null || request.MotionMode is not null || request.StrictSpeedMotionEventsPerSecond is not null || request.PrecisionMotionEventsPerSecond is not null || request.MaximumMotionErrorPixels is not null || request.Loop || request.RepeatCount is not null || request.RepeatDelayMs is not null || request.CountdownSeconds is not null || request.TimeoutSeconds is not null || request.DryRun)
        {
            return CreateStartResult(McpToolOutcomeMapper.InvalidArguments("Record automation accepts outputPath and recording options only."), operation: null);
        }

        McpToolOutcome? fileWriteCapability = _authorization.Require(McpCapability.FileWrite);
        if (fileWriteCapability is not null)
        {
            return CreateStartResult(fileWriteCapability, operation: null);
        }

        if (!_pathAuthorizer.TryNormalizeRecordingOutputPath(request.OutputPath, out string? outputPath, out McpToolOutcome? pathError))
        {
            return CreateStartResult(pathError, operation: null);
        }

        if (!TryGetRecordingOptions(request, out RecordingOptions? options, out McpToolOutcome? optionsError))
        {
            return CreateStartResult(optionsError, operation: null);
        }

        CliPreflightResult preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Record, cancellationToken).ConfigureAwait(false);
        if (!preflight.Success)
        {
            return CreateStartResult(McpToolOutcomeMapper.FromPreflightResult(preflight), operation: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        McpAutomationOperationStartResult start = _operationCoordinator.Start(McpAutomationOperationKind.Record, token => ExecuteRecordAsync(outputPath, options, token), CancellationToken.None);
        return CreateStartResult(start.Error ?? McpToolOutcomeMapper.Success("Automation operation started."), start.Operation);
    }

    private async Task<CliCommandExecutionResult> ExecutePlayAsync(string macroPath, AutomationPlaybackOptions options, bool dryRun, CancellationToken cancellationToken)
    {
        MacroExecutionResult result = await RunWithTimeoutAsync(options.TimeoutSeconds, token => _macroExecutionService.ExecuteAsync(new MacroExecutionRequest { MacroFilePath = macroPath, SpeedMultiplier = options.SpeedMultiplier, Loop = options.Loop, RepeatCount = options.RepeatCount, RepeatDelayMs = options.RepeatDelayMs, MotionMode = options.MotionMode, StrictSpeedMotionEventsPerSecond = options.StrictSpeedMotionEventsPerSecond, PrecisionMotionEventsPerSecond = options.PrecisionMotionEventsPerSecond, MaximumMotionErrorPixels = options.MaximumMotionErrorPixels, CountdownSeconds = options.CountdownSeconds, DryRun = dryRun }, token), cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteRunAsync(IReadOnlyList<string> steps, string? stepFilePath, IReadOnlyList<RunImageAssetCliOption> imageAssets, RunOptions options, bool dryRun, CancellationToken cancellationToken)
    {
        MacroExecutionResult result = await RunWithTimeoutAsync(options.TimeoutSeconds, token => _runScriptExecutionService.ExecuteAsync(new RunCliExecutionRequest { Steps = steps, StepFilePath = stepFilePath, SpeedMultiplier = options.SpeedMultiplier, CountdownSeconds = options.CountdownSeconds, DryRun = dryRun, ImageAssets = imageAssets }, token), cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteRecordAsync(string outputPath, RecordingOptions options, CancellationToken cancellationToken)
    {
        RecordExecutionResult result = await _recordExecutionService.ExecuteAsync(new RecordExecutionRequest { OutputFilePath = outputPath, RecordMouse = options.RecordMouse, RecordKeyboard = options.RecordKeyboard, CoordinateMode = options.CoordinateMode, SkipInitialZero = options.SkipInitialZero, DurationSeconds = options.DurationSeconds }, cancellationToken).ConfigureAwait(false);
        return result.Success ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings) : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }

    private static async Task<MacroExecutionResult> RunWithTimeoutAsync(int timeoutSeconds, Func<CancellationToken, Task<MacroExecutionResult>> executeAsync, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            MacroExecutionResult result = await executeAsync(timeout.Token).ConfigureAwait(false);
            return timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested ? TimedOutResult() : result;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return TimedOutResult();
        }
    }

    private static CliCommandExecutionResult ToCliResult(MacroExecutionResult result) => result.Success ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings) : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);

    private static MacroExecutionResult TimedOutResult() => new() { Success = false, ExitCode = CliExitCode.RuntimeError, Message = "Automation operation timed out." };

    private static bool TryGetPlaybackOptions(AutomationRequest request, out AutomationPlaybackOptions options, out McpToolOutcome error)
    {
        options = new(1, Loop: false, 1, 0, 0, DefaultAutomationTimeoutSeconds, MotionPlaybackMode.Precision, PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond, PlaybackOptions.DefaultPrecisionMotionEventsPerSecond, PlaybackOptions.DefaultMaximumMotionErrorPixels);
        double speed = request.SpeedMultiplier ?? 1d;
        if (!double.IsFinite(speed) || speed is < PlaybackOptions.MinSpeedMultiplier or > PlaybackOptions.MaxSpeedMultiplier)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation speedMultiplier must be a finite number between 0.1 and 10.");
            return false;
        }

        int repeat = request.RepeatCount ?? 1;
        if (repeat < 0 || (repeat is 0 && !request.Loop))
        {
            error = McpToolOutcomeMapper.InvalidArguments(repeat < 0 ? "Automation repeatCount must be non-negative." : "Automation repeatCount of 0 requires loop to be true.");
            return false;
        }

        int repeatDelay = request.RepeatDelayMs ?? 0;
        if (repeatDelay is < 0 or > MaximumAutomationRepeatDelayMs)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation repeatDelayMs must be between 0 and 3600000.");
            return false;
        }

        if (!TryGetSeconds(request.CountdownSeconds, "countdownSeconds", 0, allowZero: true, out int countdown, out error) || !TryGetSeconds(request.TimeoutSeconds, "timeoutSeconds", DefaultAutomationTimeoutSeconds, allowZero: false, out int timeout, out error))
        {
            return false;
        }

        MotionPlaybackMode motionMode = request.MotionMode?.Trim().ToLowerInvariant() switch { null or "" or "precision" => MotionPlaybackMode.Precision, "strict-speed" or "strictspeed" => MotionPlaybackMode.StrictSpeed, _ => (MotionPlaybackMode)(-1) };
        int strictRate = request.StrictSpeedMotionEventsPerSecond ?? PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond;
        int precisionRate = request.PrecisionMotionEventsPerSecond ?? PlaybackOptions.DefaultPrecisionMotionEventsPerSecond;
        double maximumError = request.MaximumMotionErrorPixels ?? PlaybackOptions.DefaultMaximumMotionErrorPixels;
        if (!Enum.IsDefined(motionMode))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation motionMode must be precision or strict-speed.");
            return false;
        }

        if (strictRate is < PlaybackOptions.MinStrictSpeedMotionEventsPerSecond or > PlaybackOptions.MaxStrictSpeedMotionEventsPerSecond || precisionRate is < PlaybackOptions.MinPrecisionMotionEventsPerSecond or > PlaybackOptions.MaxPrecisionMotionEventsPerSecond || !double.IsFinite(maximumError) || maximumError is < PlaybackOptions.MinMaximumMotionErrorPixels or > PlaybackOptions.MaxMaximumMotionErrorPixels)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation motion options are outside the supported ranges.");
            return false;
        }

        options = new(speed, request.Loop || repeat is not 1, repeat, repeatDelay, countdown, timeout, motionMode, strictRate, precisionRate, maximumError);
        return true;
    }

    private static bool TryGetRunOptions(AutomationRequest request, out RunOptions options, out McpToolOutcome error)
    {
        options = new(1, 0, DefaultAutomationTimeoutSeconds);
        double speed = request.SpeedMultiplier ?? 1d;
        if (!double.IsFinite(speed) || speed is < PlaybackOptions.MinSpeedMultiplier or > PlaybackOptions.MaxSpeedMultiplier)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation speedMultiplier must be a finite number between 0.1 and 10.");
            return false;
        }

        if (!TryGetSeconds(request.CountdownSeconds, "countdownSeconds", 0, allowZero: true, out int countdown, out error) || !TryGetSeconds(request.TimeoutSeconds, "timeoutSeconds", DefaultAutomationTimeoutSeconds, allowZero: false, out int timeout, out error))
        {
            return false;
        }

        options = new(speed, countdown, timeout);
        return true;
    }

    private static bool TryGetRecordingOptions(AutomationRequest request, out RecordingOptions options, out McpToolOutcome error)
    {
        bool mouse = request.RecordMouse ?? true;
        bool keyboard = request.RecordKeyboard ?? true;
        int duration = request.DurationSeconds ?? DefaultAutomationTimeoutSeconds;
        options = new(mouse, keyboard, RecordCoordinateMode.Auto, request.SkipInitialZero, duration);
        if (!mouse && !keyboard)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation recording requires mouse or keyboard capture.");
            return false;
        }

        RecordCoordinateMode coordinateMode = request.CoordinateMode?.Trim().ToLowerInvariant() switch { null or "" or "auto" => RecordCoordinateMode.Auto, "absolute" => RecordCoordinateMode.Absolute, "relative" => RecordCoordinateMode.Relative, _ => (RecordCoordinateMode)(-1) };
        if (!Enum.IsDefined(coordinateMode))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation coordinateMode must be auto, absolute, or relative.");
            return false;
        }

        if (duration is <= 0 or > MaximumAutomationRecordDurationSeconds)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation durationSeconds must be between 1 and 3600.");
            return false;
        }

        options = new(mouse, keyboard, coordinateMode, request.SkipInitialZero, duration);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetSeconds(int? value, string argumentName, int defaultValue, bool allowZero, out int seconds, out McpToolOutcome error)
    {
        seconds = value ?? defaultValue;
        if (seconds is < 0 or > MaximumAutomationTimeoutSeconds || (!allowZero && seconds is 0))
        {
            error = McpToolOutcomeMapper.InvalidArguments($"Automation {argumentName} must be between {(allowZero ? 0 : 1).ToString(System.Globalization.CultureInfo.InvariantCulture)} and 3600.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryValidateSteps(IReadOnlyList<string> steps, out IReadOnlyList<string> normalizedSteps, out McpToolOutcome error)
    {
        normalizedSteps = [];
        if (steps.Count is 0)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Run automation requires at least one step.");
            return false;
        }

        if (steps.Count > MaximumAutomationStepCount)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Run automation exceeds the maximum step count.");
            return false;
        }

        int totalCharacters = 0;
        string[] normalized = new string[steps.Count];
        for (int index = 0; index < steps.Count; index++)
        {
            string step = steps[index];
            if (string.IsNullOrWhiteSpace(step) || step.Length > MaximumAutomationStepCharacters)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Run automation steps must be non-empty and at most 16384 characters.");
                return false;
            }

            totalCharacters = checked(totalCharacters + step.Length);
            if (totalCharacters > MaximumAutomationStepPayloadCharacters)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Run automation steps exceed the maximum payload size.");
                return false;
            }

            normalized[index] = step;
        }

        normalizedSteps = normalized;
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private AssetNormalizationResult NormalizeImageAssets(IReadOnlyList<McpRunImageAsset>? assets)
    {
        if (assets is null or { Count: 0 })
        {
            return new(Success: true, [], Error: null);
        }

        if (assets.Count > 100)
        {
            return new(Success: false, [], McpToolOutcomeMapper.InvalidArguments("Run image assets exceed the maximum count."));
        }

        var normalized = new List<RunImageAssetCliOption>(assets.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (McpRunImageAsset asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Name) || !asset.Name.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_') || !char.IsAsciiLetter(asset.Name[0]))
            {
                return new(Success: false, [], McpToolOutcomeMapper.InvalidArguments("Run image asset names must match [A-Za-z_][A-Za-z0-9_]*."));
            }

            if (!names.Add(asset.Name))
            {
                return new(Success: false, [], McpToolOutcomeMapper.InvalidArguments("Run image asset names must be unique."));
            }

            if (!_pathAuthorizer.TryAuthorizeImageOrMacroReadPath(asset.FilePath, out string? path, out McpToolOutcome? pathError))
            {
                return new(Success: false, [], pathError);
            }

            normalized.Add(new RunImageAssetCliOption(asset.Name, path));
        }

        return new(Success: true, normalized, Error: null);
    }

    private static bool IsValidOperationId(string operationId) => operationId.Length is 32 && operationId.All(static character => char.IsAsciiHexDigit(character));

    private static CallToolResult CreateStartResult(McpToolOutcome outcome, McpAutomationOperation? operation) => CreateResult(new McpAutomationStartResult(outcome, operation), FormatOperationMessage(outcome.Message, operation));
    private static CallToolResult CreateGetResult(McpToolOutcome outcome, McpAutomationOperation? operation) => CreateResult(new McpAutomationGetResult(outcome, operation), FormatOperationMessage(outcome.Message, operation));
    private static CallToolResult CreateStopResult(McpToolOutcome outcome, McpAutomationOperation? operation, bool cancellationInitiated) => CreateResult(new McpAutomationStopResult(outcome, operation, cancellationInitiated), $"{FormatOperationMessage(outcome.Message, operation)} Cancellation initiated: {cancellationInitiated}.");
    private static CallToolResult CreateResult(McpAutomationStartResult result, string message) => CreateResult(result.Outcome, message, JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpAutomationStartResult));
    private static CallToolResult CreateResult(McpAutomationGetResult result, string message) => CreateResult(result.Outcome, message, JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpAutomationGetResult));
    private static CallToolResult CreateResult(McpAutomationStopResult result, string message) => CreateResult(result.Outcome, message, JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpAutomationStopResult));
    private static CallToolResult CreateResult(McpToolOutcome outcome, string message, JsonElement structuredContent) => new() { Content = [new TextContentBlock { Text = message }], StructuredContent = structuredContent, IsError = !outcome.Success };
    private static string FormatOperationMessage(string message, McpAutomationOperation? operation)
    {
        if (operation is null)
        {
            return message;
        }

        var outcome = operation.Outcome is null
            ? string.Empty
            : $" Outcome: {operation.Outcome.Message} (code {operation.Outcome.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}).";
        return $"{message} Operation ID: {operation.OperationId}; state: {operation.State}; completedAt: {operation.CompletedAt?.ToString("O") ?? "pending"}.{outcome}";
    }

    private sealed record AutomationRequest(string? MacroPath, IReadOnlyList<string>? Steps, string? OutputPath, string? StepFilePath, IReadOnlyList<McpRunImageAsset>? ImageAssets, double? SpeedMultiplier, bool Loop, int? RepeatCount, int? RepeatDelayMs, int? CountdownSeconds, int? TimeoutSeconds, int? DurationSeconds, bool DryRun, bool? RecordMouse, bool? RecordKeyboard, string? CoordinateMode, string? MotionMode, int? StrictSpeedMotionEventsPerSecond, int? PrecisionMotionEventsPerSecond, double? MaximumMotionErrorPixels, bool SkipInitialZero);
    private sealed record AutomationPlaybackOptions(double SpeedMultiplier, bool Loop, int RepeatCount, int RepeatDelayMs, int CountdownSeconds, int TimeoutSeconds, MotionPlaybackMode MotionMode, int StrictSpeedMotionEventsPerSecond, int PrecisionMotionEventsPerSecond, double MaximumMotionErrorPixels);
    private sealed record RunOptions(double SpeedMultiplier, int CountdownSeconds, int TimeoutSeconds);
    private sealed record RecordingOptions(bool RecordMouse, bool RecordKeyboard, RecordCoordinateMode CoordinateMode, bool SkipInitialZero, int DurationSeconds);
    private sealed record AssetNormalizationResult(bool Success, IReadOnlyList<RunImageAssetCliOption> Assets, McpToolOutcome? Error);
}
