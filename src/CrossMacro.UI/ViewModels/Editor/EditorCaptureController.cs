using CrossMacro.Platform.Abstractions.Input.Capture;

namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Captures mouse positions, keys and screen colors into the selected editor action. Every capture shares
/// one lifecycle: a prompt, a cancellable wait, a UI-thread apply that is skipped when the selection
/// moved meanwhile, and a mode reset.
/// </summary>
internal sealed class EditorCaptureController(
    EditorViewModel owner,
    EditorCaptureSession session,
    ICoordinateCaptureService captureService,
    IKeyCodeMapper keyCodeMapper,
    IScreenPixelReader? screenPixelReader,
    IUiDispatcher uiDispatcher)
{
    internal Task CaptureMouseAsync() =>
        CaptureAsync(EditorCaptureMode.Position, "Editor_StatusCaptureMousePrompt", targetCheck: null, async (target, token) =>
        {
            var result = await captureService.CaptureMousePositionAsync(token).ConfigureAwait(false);
            await ApplyAsync(target, result, point => ApplyMousePosition(target, point.X, point.Y)).ConfigureAwait(false);
        });

    internal Task CaptureKeyAsync() =>
        CaptureAsync(EditorCaptureMode.Key, "Editor_StatusCaptureKeyPrompt", targetCheck: null, async (target, token) =>
        {
            var result = await captureService.CaptureKeyCodeAsync(token).ConfigureAwait(false);
            await ApplyAsync(target, result, keyCode =>
            {
                target.KeyCode = keyCode;
                target.KeyName = keyCodeMapper.GetKeyName(keyCode);
                owner.Status = owner.LocalizeFormat("Editor_StatusCapturedKey", target.KeyName, keyCode);
            }).ConfigureAwait(false);
        });

    internal Task CaptureTargetColorAsync() =>
        CaptureColorAsync(
            EditorCaptureMode.TargetColor,
            static action => action.Type is EditorActionType.WaitColor or EditorActionType.PixelSearch,
            recheckTarget: false,
            (action, color) => action.ScreenColorHex = color);

    internal Task CaptureConditionLeftColorAsync() =>
        CaptureColorAsync(
            EditorCaptureMode.ConditionLeftColor,
            static action => IsConditionColorTarget(action, static a => a.ScriptLeftOperandType),
            recheckTarget: true,
            (action, color) => action.ScriptLeftOperand = color);

    internal Task CaptureConditionRightColorAsync() =>
        CaptureColorAsync(
            EditorCaptureMode.ConditionRightColor,
            static action => IsConditionColorTarget(action, static a => a.ScriptRightOperandType),
            recheckTarget: true,
            (action, color) => action.ScriptRightOperand = color);

    internal Task CapturePixelSearchTopLeftAsync() =>
        CaptureRegionPointAsync(EditorCaptureMode.PixelSearchTopLeft, IsPixelSearchRegionTarget, ApplyPixelSearchTopLeft);

    internal Task CapturePixelSearchBottomRightAsync() =>
        CaptureRegionPointAsync(EditorCaptureMode.PixelSearchBottomRight, IsPixelSearchRegionTarget, ApplyPixelSearchBottomRight);

    internal Task CaptureScreenshotRegionStartAsync() =>
        CaptureRegionPointAsync(EditorCaptureMode.ScreenshotRegionStart, IsScreenshotTarget, ApplyScreenshotRegionStart);

    internal Task CaptureScreenshotRegionEndAsync() =>
        CaptureRegionPointAsync(EditorCaptureMode.ScreenshotRegionEnd, IsScreenshotTarget, ApplyScreenshotRegionEnd);

    internal void Cancel()
    {
        session.Cancel();
        owner.CaptureMode = EditorCaptureMode.None;
        owner.Status = owner.Localize("Editor_StatusCaptureCancelled");
    }

    private static bool IsConditionColorTarget(EditorAction action, Func<EditorAction, ScriptOperandType> getOperandType)
    {
        return action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
            && getOperandType(action) is ScriptOperandType.Color;
    }

    private static bool IsPixelSearchRegionTarget(EditorAction action)
    {
        return action.Type is EditorActionType.PixelSearch
            || (action.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage
                && action.TryGetLiteralImageSearchRegion(out _, out _, out _, out _));
    }

    private static bool IsScreenshotTarget(EditorAction action) => action.Type is EditorActionType.Screenshot;

    private static bool TryParseInteger(string token, out int value)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsPositiveIntegerOrVariable(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value > 0
            : token.StartsWith('$') && ScriptIdentifierSyntax.IsValidVariableName(token);
    }

    private void ApplyMousePosition(EditorAction target, int x, int y)
    {
        if (target.Type is EditorActionType.PixelColor or EditorActionType.WaitColor)
        {
            if (target.Type is EditorActionType.PixelColor)
            {
                target.IsAbsolute = true;
            }

            target.ScreenX = x;
            target.ScreenY = y;
        }
        else
        {
            target.UseCurrentPosition = false;
            target.IsAbsolute = true;
            target.X = x;
            target.Y = y;
        }

        owner.Status = owner.LocalizeFormat("Editor_StatusCapturedPosition", x, y);
    }

    private void ApplyPixelSearchTopLeft(EditorAction action, int x, int y)
    {
        var previousWidth = Math.Max(1, action.ScreenWidth);
        var previousHeight = Math.Max(1, action.ScreenHeight);
        var existingRight = action.ScreenLeft + previousWidth - 1;
        var existingBottom = action.ScreenTop + previousHeight - 1;

        action.ScreenLeft = x;
        action.ScreenTop = y;
        action.ScreenWidth = existingRight >= x ? existingRight - x + 1 : previousWidth;
        action.ScreenHeight = existingBottom >= y ? existingBottom - y + 1 : previousHeight;
        owner.Status = owner.LocalizeFormat("Editor_StatusCapturedRegionTopLeft", x, y);
    }

    private void ApplyPixelSearchBottomRight(EditorAction action, int x, int y)
    {
        var width = x - action.ScreenLeft + 1;
        var height = y - action.ScreenTop + 1;
        if (width <= 0 || height <= 0)
        {
            owner.Status = owner.Localize("Editor_StatusCaptureRegionInvalidBottomRight");
            return;
        }

        action.ScreenWidth = width;
        action.ScreenHeight = height;
        owner.Status = owner.LocalizeFormat("Editor_StatusCapturedRegionBottomRight", x, y);
    }

    private void ApplyScreenshotRegionStart(EditorAction action, int x, int y)
    {
        action.ScreenshotUseRegion = true;
        action.ScreenshotRegionX = x.ToString(CultureInfo.InvariantCulture);
        action.ScreenshotRegionY = y.ToString(CultureInfo.InvariantCulture);

        if (!IsPositiveIntegerOrVariable(action.ScreenshotRegionWidth))
        {
            action.ScreenshotRegionWidth = "1";
        }

        if (!IsPositiveIntegerOrVariable(action.ScreenshotRegionHeight))
        {
            action.ScreenshotRegionHeight = "1";
        }

        owner.Status = owner.LocalizeFormat("Editor_StatusCapturedRegionTopLeft", x, y);
    }

    private void ApplyScreenshotRegionEnd(EditorAction action, int endX, int endY)
    {
        var startX = TryParseInteger(action.ScreenshotRegionX, out var parsedStartX) ? parsedStartX : endX;
        var startY = TryParseInteger(action.ScreenshotRegionY, out var parsedStartY) ? parsedStartY : endY;
        var widthValue = Math.Abs((long)endX - startX) + 1;
        var heightValue = Math.Abs((long)endY - startY) + 1;
        if (widthValue > int.MaxValue || heightValue > int.MaxValue)
        {
            owner.Status = owner.Localize("Editor_StatusCaptureRegionInvalidBottomRight");
            return;
        }

        action.ScreenshotUseRegion = true;
        action.ScreenshotRegionX = Math.Min(startX, endX).ToString(CultureInfo.InvariantCulture);
        action.ScreenshotRegionY = Math.Min(startY, endY).ToString(CultureInfo.InvariantCulture);
        action.ScreenshotRegionWidth = Math.Max(1L, widthValue).ToString(CultureInfo.InvariantCulture);
        action.ScreenshotRegionHeight = Math.Max(1L, heightValue).ToString(CultureInfo.InvariantCulture);
        owner.Status = owner.LocalizeFormat("Editor_StatusCapturedRegionBottomRight", endX, endY);
    }

    private Task CaptureRegionPointAsync(EditorCaptureMode mode, Func<EditorAction, bool> isValidTarget, Action<EditorAction, int, int> applyPoint) =>
        CaptureAsync(mode, "Editor_StatusCaptureMousePrompt", isValidTarget, async (target, token) =>
        {
            var result = await captureService.CaptureMousePositionAsync(token).ConfigureAwait(false);
            await ApplyAsync(target, result, point => applyPoint(target, point.X, point.Y)).ConfigureAwait(false);
        });

    /// <summary>
    /// Captures a position, reads the pixel under it and stores the color. The selection and the target
    /// check are re-validated before the pixel is read and again before it is stored.
    /// </summary>
    private async Task CaptureColorAsync(EditorCaptureMode mode, Func<EditorAction, bool> isValidTarget, bool recheckTarget, Action<EditorAction, string> storeColor)
    {
        if (screenPixelReader is not { IsSupported: true } pixelReader)
        {
            if (TryGetTarget(isValidTarget) is not null)
            {
                owner.Status = owner.Localize("Editor_StatusPixelReaderUnavailable");
            }

            return;
        }

        await CaptureAsync(mode, "Editor_StatusCaptureColorPrompt", isValidTarget, async (target, token) =>
        {
            var position = await captureService.CaptureMousePositionAsync(token).ConfigureAwait(false);
            if (position is null)
            {
                await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.Localize("Editor_StatusCaptureCancelled")).ConfigureAwait(false);
                return;
            }

            var canReadPixel = false;
            var recheck = recheckTarget ? isValidTarget : null;
            await uiDispatcher.RunOnUiThreadAsync(() => canReadPixel = IsStillValid(target, recheck)).ConfigureAwait(false);
            if (!canReadPixel)
            {
                return;
            }

            var pixel = await pixelReader.GetPixelAsync(
                new ScreenPoint(position.Value.X, position.Value.Y),
                new ScreenReadOptions(cancellationToken: token)).ConfigureAwait(false);
            await uiDispatcher.RunOnUiThreadAsync(() =>
            {
                if (!IsStillValid(target, recheck))
                {
                    return;
                }

                if (!pixel.IsSuccess)
                {
                    owner.Status = owner.LocalizeFormat(
                        "Editor_StatusCaptureColorFailed",
                        pixel.ErrorMessage ?? owner.Localize("Editor_StatusPixelReaderUnavailable"));
                    return;
                }

                storeColor(target, pixel.Value.ToString());
                owner.Status = owner.LocalizeFormat("Editor_StatusCapturedColor", pixel.Value.ToString(), position.Value.X, position.Value.Y);
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private EditorAction? TryGetTarget(Func<EditorAction, bool>? isValidTarget)
    {
        var target = owner.SelectedAction;
        if (target is null)
        {
            owner.Status = owner.Localize("Editor_StatusSelectActionFirst");
            return null;
        }

        if (isValidTarget is not null && !isValidTarget(target))
        {
            owner.Status = owner.Localize("Editor_StatusOperationBlocked");
            return null;
        }

        return target;
    }

    /// <summary>Re-checks, on the UI thread, that the captured-for action is still selected and, when asked, still a valid target.</summary>
    private bool IsStillValid(EditorAction target, Func<EditorAction, bool>? recheckTarget)
    {
        if (!ReferenceEquals(owner.SelectedAction, target))
        {
            owner.Status = owner.Localize("Editor_StatusCaptureSelectionChanged");
            return false;
        }

        if (recheckTarget is not null && !recheckTarget(target))
        {
            owner.Status = owner.Localize("Editor_StatusOperationBlocked");
            return false;
        }

        return true;
    }

    /// <summary>Applies a captured value on the UI thread unless the capture was cancelled or the selection moved.</summary>
    private Task ApplyAsync<T>(EditorAction target, T? result, Action<T> apply)
        where T : struct
    {
        return uiDispatcher.RunOnUiThreadAsync(() =>
        {
            if (result is null)
            {
                owner.Status = owner.Localize("Editor_StatusCaptureCancelled");
                return;
            }

            if (!ReferenceEquals(owner.SelectedAction, target))
            {
                owner.Status = owner.Localize("Editor_StatusCaptureSelectionChanged");
                return;
            }

            apply(result.Value);
        });
    }

    private async Task CaptureAsync(
        EditorCaptureMode mode,
        string promptKey,
        Func<EditorAction, bool>? targetCheck,
        Func<EditorAction, CancellationToken, Task> capture)
    {
        var target = TryGetTarget(targetCheck);
        if (target is null)
        {
            return;
        }

        owner.CaptureMode = mode;
        owner.Status = owner.Localize(promptKey);

        try
        {
            using var cancellationTokenSource = session.Begin();
            await capture(target, cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.LocalizeFormat("Editor_StatusCaptureError", ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await uiDispatcher.RunOnUiThreadAsync(() => owner.CaptureMode = EditorCaptureMode.None).ConfigureAwait(false);
        }
    }
}
