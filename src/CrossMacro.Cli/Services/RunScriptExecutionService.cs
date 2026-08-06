
namespace CrossMacro.Cli.Services;

public sealed class RunScriptExecutionService(IRunExecutionService runtimeService, IImageAssetCodec? imageAssetCodec = null) : IRunScriptExecutionService
{
    private readonly IRunExecutionService _runtimeService = runtimeService ?? throw new ArgumentNullException(nameof(runtimeService));
    private readonly IImageAssetCodec? _imageAssetCodec = imageAssetCodec;

    public async Task<MacroExecutionResult> ExecuteAsync(
        RunCliExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var loadResult = await RunStepLoader.LoadAsync(request, cancellationToken).ConfigureAwait(false);
        if (!loadResult.Success)
        {
            return loadResult.ErrorResult!;
        }

        var steps = loadResult.Steps!;
        if (steps.Count is 0)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "No run steps provided.",
                Errors = ["Use --step at least once."],
            };
        }

        var imageAssetsResult = await LoadImageAssetsAsync(request.ImageAssets, cancellationToken).ConfigureAwait(false);
        if (!imageAssetsResult.Success)
        {
            return imageAssetsResult.ErrorResult!;
        }

        var result = await _runtimeService.ExecuteAsync(new Application.Runtime.RunExecutionRequest(
            steps.Select(step => new RunScriptInputStep(step.Step, step.FileLineNumber, step.SourceIndex)).ToList(),
            request.SpeedMultiplier,
            request.CountdownSeconds,
            request.DryRun,
            imageAssetsResult.Images), cancellationToken).ConfigureAwait(false);

        if (result.Status is RunExecutionStatus.InvalidArguments)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Run script parsing failed.",
                Errors = result.Errors,
            };
        }

        var data = result.Sequence is null
            ? null
            : BuildData(result.Sequence, result.StepCount, result, result.RuntimeVariables);

        return result.Status switch
        {
            RunExecutionStatus.ValidationFailed => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.ValidationError,
                Message = "Run script validation failed.",
                Errors = result.Errors,
                Warnings = result.Warnings,
                Data = data,
            },
            RunExecutionStatus.Cancelled => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.Cancelled,
                Message = "Run script execution cancelled.",
            },
            RunExecutionStatus.AbsolutePlaybackUnsupported => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Absolute coordinate playback is not supported in this session.",
                Errors = ["This run script contains absolute mouse coordinates, but the active backend cannot play absolute coordinates. Use a backend/session with absolute coordinate support or change the script to use relative coordinates."],
                Warnings = result.Warnings,
                Data = data,
            },
            RunExecutionStatus.InputInjectionPermissionRequired => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.EnvironmentError,
                Message = "Playback permission is missing.",
                Errors = [result.ErrorMessage ?? "macOS playback permission is missing."],
                Warnings = result.Warnings,
                Data = data,
            },
            RunExecutionStatus.Succeeded when request.DryRun => new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script parsed successfully (dry-run).",
                Warnings = result.Warnings,
                Data = data,
            },
            RunExecutionStatus.Succeeded => new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run script execution complete.",
                Warnings = result.Warnings,
                Data = data,
            },
            RunExecutionStatus.InvalidArguments => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.InvalidArguments,
                Message = "Run script execution failed: invalid arguments.",
                Errors = [result.ErrorMessage ?? "Invalid arguments."],
                Warnings = result.Warnings,
                Data = data,
            },
            RunExecutionStatus.Failed => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Run script execution failed.",
                Errors = [result.ErrorMessage ?? "Unknown runtime error."],
                Warnings = result.Warnings,
                Data = data,
            },
            _ => new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Run script execution failed.",
                Errors = [result.ErrorMessage ?? "Unknown runtime error."],
                Warnings = result.Warnings,
                Data = data,
            },
        };
    }

    private async Task<RunImageAssetLoadOutcome> LoadImageAssetsAsync(
        IReadOnlyList<RunImageAssetCliOption> requestedAssets,
        CancellationToken cancellationToken)
    {
        if (requestedAssets.Count is 0)
        {
            return RunImageAssetLoadOutcome.Ok(images: null);
        }

        if (_imageAssetCodec is null)
        {
            return RunImageAssetLoadOutcome.Fail(new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.EnvironmentError,
                Message = "Run image assets are not supported in this runtime.",
                Errors = ["No image asset codec is available."],
            });
        }

        var images = new Dictionary<string, string>(StringComparer.Ordinal);
        long totalEncodedBytes = 0;
        foreach (var asset in requestedAssets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var assetName = asset.Name?.Trim();
            if (string.IsNullOrWhiteSpace(assetName)
                || assetName.StartsWith('$')
                || !EditorActionScriptTokens.IsValidVariableName(assetName))
            {
                return RunImageAssetLoadOutcome.Fail(new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.InvalidArguments,
                    Message = "Invalid run image asset name.",
                    Errors = [$"Invalid asset name '{asset.Name}'. Expected [A-Za-z_][A-Za-z0-9_]*."],
                });
            }

            try
            {
                var bytes = await _imageAssetCodec.ReadFileAsync(asset.FilePath, assetName, cancellationToken).ConfigureAwait(false);
                using var frame = await _imageAssetCodec.DecodePngAsync(bytes, assetName, cancellationToken).ConfigureAwait(false);
                totalEncodedBytes = checked(totalEncodedBytes + bytes.LongLength);
                _imageAssetCodec.ValidateMacroBudget(totalEncodedBytes);
                images.Add(assetName, Convert.ToBase64String(bytes));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                return RunImageAssetLoadOutcome.Fail(new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = File.Exists(asset.FilePath) ? CliExitCode.InvalidArguments : CliExitCode.FileError,
                    Message = "Failed to load run image asset.",
                    Errors = [$"{assetName}: {ex.Message}"],
                });
            }
        }

        return RunImageAssetLoadOutcome.Ok(images);
    }

    private static RunScriptExecutionData BuildData(
        MacroSequence sequence,
        int stepCount,
        Application.Runtime.RunExecutionResult result,
        IReadOnlyDictionary<string, string>? runtimeVariables = null)
    {
        var coordinateMode = MacroPositionSemantics.GetCoordinateModeSummary(sequence) switch
        {
            CoordinateModeSummary.Absolute => "absolute",
            CoordinateModeSummary.Relative => "relative",
            CoordinateModeSummary.Mixed => "mixed",
            CoordinateModeSummary.None => "none",
            _ => "none",
        };

        return new RunScriptExecutionData(
            stepCount,
            sequence.EventCount,
            sequence.TotalDurationMs,
            result.InitialDelayMs,
            result.InitialHasRandomDelay,
            result.InitialRandomDelayMinMs,
            result.InitialRandomDelayMaxMs,
            sequence.TrailingDelayMs,
            coordinateMode,
            runtimeVariables ?? new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
