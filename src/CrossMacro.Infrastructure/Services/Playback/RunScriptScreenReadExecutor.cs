using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptScreenReadExecutor
{
    private readonly IScreenPixelReader _screenPixelReader;
    private readonly IMousePositionProvider? _mousePositionProvider;

    public RunScriptScreenReadExecutor(
        IScreenPixelReader screenPixelReader,
        IMousePositionProvider? mousePositionProvider)
    {
        _screenPixelReader = screenPixelReader ?? throw new ArgumentNullException(nameof(screenPixelReader));
        _mousePositionProvider = mousePositionProvider;
    }

    public async Task ExecuteAsync(
        MacroSequence macro,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(runtimeVariables);

        for (var i = 0; i < macro.ScriptSteps.Count; i++)
        {
            await ExecuteStepAsync(macro.ScriptSteps[i], i + 1, runtimeVariables, cancellationToken);
        }
    }

    public async Task ExecuteStepAsync(
        string step,
        int stepNumber,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(runtimeVariables);

        cancellationToken.ThrowIfCancellationRequested();

        var trimmedStep = step.Trim();
        if (trimmedStep.Length == 0)
        {
            return;
        }

        if (!RunScriptScreenReadingStepParser.TryParseCommand(trimmedStep, out var command, out var parts))
        {
            return;
        }

        if (command == RunScriptScreenReadingCommand.PixelColor)
        {
            await ExecutePixelColorAsync(stepNumber, parts, runtimeVariables, cancellationToken);
            return;
        }

        if (command == RunScriptScreenReadingCommand.WaitColor)
        {
            await ExecuteWaitColorAsync(stepNumber, parts, runtimeVariables, cancellationToken);
            return;
        }

        if (command == RunScriptScreenReadingCommand.PixelSearch)
        {
            await ExecutePixelSearchAsync(stepNumber, parts, runtimeVariables, cancellationToken);
        }
    }

    internal static bool IsScreenReadingStep(string step)
    {
        return RunScriptSyntax.IsScreenReadingStep(step);
    }

    private async Task ExecutePixelColorAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var isRelative = parts.Count > 1 && string.Equals(parts[1], "rel", StringComparison.OrdinalIgnoreCase);
        var coordinateIndex = isRelative ? 2 : 1;
        var x = ParseInteger(parts[coordinateIndex]);
        var y = ParseInteger(parts[coordinateIndex + 1]);
        var point = isRelative
            ? await ResolveRelativePointAsync(stepNumber, x, y, cancellationToken)
            : new ScreenPoint(x, y);

        var result = await _screenPixelReader.GetPixelAsync(point, CreateOptions(null, cancellationToken));
        EnsureSuccess(stepNumber, "pixelcolor", result);

        var variableIndex = isRelative ? 4 : 3;
        if (parts.Count > variableIndex)
        {
            runtimeVariables[parts[variableIndex]] = result.Value.ToString();
        }
    }

    private async Task ExecuteWaitColorAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var point = new ScreenPoint(ParseInteger(parts[1]), ParseInteger(parts[2]));
        var expected = ResolveTargetColor(parts[3], stepNumber, runtimeVariables);
        var timeout = parts.Count >= 5
            ? TimeSpan.FromMilliseconds(ParseInteger(parts[4]))
            : (TimeSpan?)null;
        var resultVariable = parts.Count >= 6 ? parts[5] : null;

        var result = await _screenPixelReader.WaitForPixelAsync(point, expected, CreateOptions(timeout, cancellationToken));
        if (resultVariable != null && CanStoreResultVariable(result))
        {
            runtimeVariables[resultVariable] = result.IsSuccess ? "true" : "false";
            return;
        }

        EnsureSuccess(stepNumber, "waitcolor", result);
    }

    private async Task ExecutePixelSearchAsync(
        int stepNumber,
        IReadOnlyList<string> parts,
        IDictionary<string, string> runtimeVariables,
        CancellationToken cancellationToken)
    {
        var x1 = ParseInteger(parts[1]);
        var y1 = ParseInteger(parts[2]);
        var x2 = ParseInteger(parts[3]);
        var y2 = ParseInteger(parts[4]);
        var expected = ResolveTargetColor(parts[5], stepNumber, runtimeVariables);
        var tolerance = ParsePixelSearchTolerance(parts);
        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var right = Math.Max(x1, x2);
        var bottom = Math.Max(y1, y2);
        var width = checked(right - left);
        var height = checked(bottom - top);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"Step {stepNumber}: pixelsearch failed: bounds must be end-exclusive and produce a positive region.");
        }

        var region = new ScreenRect(left, top, width, height);

        var result = await _screenPixelReader.SearchPixelAsync(region, expected, tolerance, CreateOptions(null, cancellationToken));
        var variableLayout = GetPixelSearchVariableLayout(parts);
        if (variableLayout.FoundVariableName != null && CanStoreResultVariable(result))
        {
            runtimeVariables[variableLayout.FoundVariableName] = result.IsSuccess ? "true" : "false";
            runtimeVariables[variableLayout.XVariableName!] = result.IsSuccess
                ? result.Value.Point.X.ToString(CultureInfo.InvariantCulture)
                : "-1";
            runtimeVariables[variableLayout.YVariableName!] = result.IsSuccess
                ? result.Value.Point.Y.ToString(CultureInfo.InvariantCulture)
                : "-1";
            return;
        }

        EnsureSuccess(stepNumber, "pixelsearch", result);

        if (variableLayout.XVariableName != null)
        {
            runtimeVariables[variableLayout.XVariableName] = result.Value.Point.X.ToString(CultureInfo.InvariantCulture);
            runtimeVariables[variableLayout.YVariableName!] = result.Value.Point.Y.ToString(CultureInfo.InvariantCulture);
        }
    }

    private async Task<ScreenPoint> ResolveRelativePointAsync(
        int stepNumber,
        int dx,
        int dy,
        CancellationToken cancellationToken)
    {
        if (_mousePositionProvider is null)
        {
            throw new InvalidOperationException($"Step {stepNumber}: pixelcolor rel failed: no mouse position provider is available.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync();
        if (position is null)
        {
            throw new InvalidOperationException($"Step {stepNumber}: pixelcolor rel failed: current mouse position is unavailable.");
        }

        return new ScreenPoint(checked(position.Value.X + dx), checked(position.Value.Y + dy));
    }

    private static ScreenReadOptions CreateOptions(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        return new ScreenReadOptions(
            timeout ?? ScreenReadOptions.Default.Timeout,
            ScreenReadOptions.Default.PollInterval,
            cancellationToken);
    }

    private static void EnsureSuccess<T>(int stepNumber, string command, ScreenReadResult<T> result)
    {
        if (result.IsSuccess)
        {
            return;
        }

        if (result.ErrorKind == ScreenReadErrorKind.Canceled)
        {
            throw new OperationCanceledException(result.ErrorMessage);
        }

        var message = result.ErrorMessage ?? "Unknown screen read error.";
        throw new InvalidOperationException($"Step {stepNumber}: {command} failed: {result.ErrorKind}: {message}");
    }

    private static bool CanStoreResultVariable<T>(ScreenReadResult<T> result)
    {
        return result.IsSuccess || result.ErrorKind == ScreenReadErrorKind.CaptureTimeout;
    }

    private static int ParseInteger(string value)
    {
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static ScreenPixelColor ResolveTargetColor(
        string token,
        int stepNumber,
        IDictionary<string, string> runtimeVariables)
    {
        if (ScreenPixelColor.TryParse(token, out var color))
        {
            return color;
        }

        if (!token.StartsWith("$", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Step {stepNumber}: invalid color token '{token}'. Expected RRGGBB or $variable.");
        }

        var variableName = EditorActionScriptTokens.NormalizeVariableToken(token);
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            throw new InvalidOperationException($"Step {stepNumber}: invalid color variable '{token}'. Expected $variable.");
        }

        if (!runtimeVariables.TryGetValue(variableName, out var value))
        {
            throw new InvalidOperationException($"Step {stepNumber}: color variable '{variableName}' is not defined.");
        }

        if (!ScreenPixelColor.TryParse(value, out color))
        {
            throw new InvalidOperationException($"Step {stepNumber}: color variable '{variableName}' value '{value}' is invalid. Expected RRGGBB.");
        }

        return color;
    }

    private static PixelSearchVariableLayout GetPixelSearchVariableLayout(IReadOnlyList<string> parts) =>
        RunScriptScreenReadingStepParser.GetPixelSearchVariableLayout(parts);

    private static bool HasPixelSearchVariables(IReadOnlyList<string> parts) =>
        GetPixelSearchVariableLayout(parts).XVariableName != null;

    private static int ParsePixelSearchTolerance(IReadOnlyList<string> parts)
    {
        var variableLayout = GetPixelSearchVariableLayout(parts);
        var keywordIndex = variableLayout.FoundVariableName != null ? 9 : variableLayout.XVariableName != null ? 8 : 6;
        if (parts.Count <= keywordIndex)
        {
            return 0;
        }

        return RunScriptScreenReadingStepParser.IsPixelSearchToleranceKeyword(parts[keywordIndex])
            ? ParseInteger(parts[keywordIndex + 1])
            : 0;
    }
}
