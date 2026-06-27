using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    private static async Task RunOnUiThreadAsync(Action action)
    {
        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    public async Task CaptureMouseAsync()
    {
        var targetAction = SelectedAction;
        if (targetAction == null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        CaptureMode = EditorCaptureMode.Position;
        Status = Localize("Editor_StatusCaptureMousePrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token);

            await RunOnUiThreadAsync(() =>
            {
                if (!result.HasValue)
                {
                    Status = Localize("Editor_StatusCaptureCancelled");
                    return;
                }

                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = Localize("Editor_StatusCaptureSelectionChanged");
                    return;
                }

                if (targetAction.Type is EditorActionType.PixelColor or EditorActionType.WaitColor)
                {
                    if (targetAction.Type == EditorActionType.PixelColor)
                    {
                        targetAction.IsAbsolute = true;
                    }

                    targetAction.ScreenX = result.Value.X;
                    targetAction.ScreenY = result.Value.Y;
                }
                else
                {
                    targetAction.UseCurrentPosition = false;
                    targetAction.IsAbsolute = true;
                    targetAction.X = result.Value.X;
                    targetAction.Y = result.Value.Y;
                }

                Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCapturedPosition"), result.Value.X, result.Value.Y);
            });
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message);
        }
        finally
        {
            CaptureMode = EditorCaptureMode.None;
        }
    }

    public async Task CaptureKeyAsync()
    {
        var targetAction = SelectedAction;
        if (targetAction == null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        CaptureMode = EditorCaptureMode.Key;
        Status = Localize("Editor_StatusCaptureKeyPrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureKeyCodeAsync(cancellationTokenSource.Token);

            await RunOnUiThreadAsync(() =>
            {
                if (!result.HasValue)
                {
                    Status = Localize("Editor_StatusCaptureCancelled");
                    return;
                }

                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = Localize("Editor_StatusCaptureSelectionChanged");
                    return;
                }

                targetAction.KeyCode = result.Value;
                targetAction.KeyName = _keyCodeMapper.GetKeyName(result.Value);
                Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCapturedKey"), targetAction.KeyName, result.Value);
            });
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message);
        }
        finally
        {
            CaptureMode = EditorCaptureMode.None;
        }
    }

    public async Task CaptureTargetColorAsync()
    {
        var targetAction = SelectedAction;
        if (targetAction == null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (targetAction.Type is not (EditorActionType.WaitColor or EditorActionType.PixelSearch))
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        if (_screenPixelReader is not { IsSupported: true } screenPixelReader)
        {
            Status = Localize("Editor_StatusPixelReaderUnavailable");
            return;
        }

        CaptureMode = EditorCaptureMode.TargetColor;
        Status = Localize("Editor_StatusCaptureColorPrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var positionResult = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token);

            if (!positionResult.HasValue)
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusCaptureCancelled"));
                return;
            }

            var selectionChanged = false;
            await RunOnUiThreadAsync(() =>
            {
                selectionChanged = !ReferenceEquals(SelectedAction, targetAction);
                if (selectionChanged)
                {
                    Status = Localize("Editor_StatusCaptureSelectionChanged");
                }
            });

            if (selectionChanged)
            {
                return;
            }

            var point = new ScreenPoint(positionResult.Value.X, positionResult.Value.Y);
            var pixelResult = await screenPixelReader.GetPixelAsync(point, new ScreenReadOptions(cancellationToken: cancellationTokenSource.Token));

            await RunOnUiThreadAsync(() =>
            {
                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = Localize("Editor_StatusCaptureSelectionChanged");
                    return;
                }

                if (!pixelResult.IsSuccess)
                {
                    Status = string.Format(
                        _localizationService.CurrentCulture,
                        Localize("Editor_StatusCaptureColorFailed"),
                        pixelResult.ErrorMessage ?? Localize("Editor_StatusPixelReaderUnavailable"));
                    return;
                }

                var color = pixelResult.Value;
                targetAction.ScreenColorHex = color.ToString();
                Status = string.Format(
                    _localizationService.CurrentCulture,
                    Localize("Editor_StatusCapturedColor"),
                    targetAction.ScreenColorHex,
                    positionResult.Value.X,
                    positionResult.Value.Y);
            });
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message);
        }
        finally
        {
            CaptureMode = EditorCaptureMode.None;
        }
    }

    public Task CaptureConditionLeftColorAsync()
    {
        return CaptureConditionColorAsync(
            EditorCaptureMode.ConditionLeftColor,
            action => action.ScriptLeftOperandType,
            (action, color) => action.ScriptLeftOperand = color);
    }

    public Task CaptureConditionRightColorAsync()
    {
        return CaptureConditionColorAsync(
            EditorCaptureMode.ConditionRightColor,
            action => action.ScriptRightOperandType,
            (action, color) => action.ScriptRightOperand = color);
    }

    private async Task CaptureConditionColorAsync(
        EditorCaptureMode captureMode,
        Func<EditorAction, ScriptOperandType> getOperandType,
        Action<EditorAction, string> setOperand)
    {
        var targetAction = SelectedAction;
        if (targetAction == null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (!IsConditionColorTarget(targetAction, getOperandType))
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        if (_screenPixelReader is not { IsSupported: true } screenPixelReader)
        {
            Status = Localize("Editor_StatusPixelReaderUnavailable");
            return;
        }

        CaptureMode = captureMode;
        Status = Localize("Editor_StatusCaptureColorPrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var positionResult = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token);

            if (!positionResult.HasValue)
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusCaptureCancelled"));
                return;
            }

            var canReadPixel = false;
            await RunOnUiThreadAsync(() =>
            {
                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = Localize("Editor_StatusCaptureSelectionChanged");
                    return;
                }

                if (!IsConditionColorTarget(targetAction, getOperandType))
                {
                    Status = Localize("Editor_StatusOperationBlocked");
                    return;
                }

                canReadPixel = true;
            });

            if (!canReadPixel)
            {
                return;
            }

            var point = new ScreenPoint(positionResult.Value.X, positionResult.Value.Y);
            var pixelResult = await screenPixelReader.GetPixelAsync(point, new ScreenReadOptions(cancellationToken: cancellationTokenSource.Token));

            await RunOnUiThreadAsync(() =>
            {
                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = Localize("Editor_StatusCaptureSelectionChanged");
                    return;
                }

                if (!IsConditionColorTarget(targetAction, getOperandType))
                {
                    Status = Localize("Editor_StatusOperationBlocked");
                    return;
                }

                if (!pixelResult.IsSuccess)
                {
                    Status = string.Format(
                        _localizationService.CurrentCulture,
                        Localize("Editor_StatusCaptureColorFailed"),
                        pixelResult.ErrorMessage ?? Localize("Editor_StatusPixelReaderUnavailable"));
                    return;
                }

                var color = pixelResult.Value.ToString();
                setOperand(targetAction, color);
                Status = string.Format(
                    _localizationService.CurrentCulture,
                    Localize("Editor_StatusCapturedColor"),
                    color,
                    positionResult.Value.X,
                    positionResult.Value.Y);
            });
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message);
        }
        finally
        {
            CaptureMode = EditorCaptureMode.None;
        }
    }

    private static bool IsConditionColorTarget(EditorAction action, Func<EditorAction, ScriptOperandType> getOperandType)
    {
        return action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
            && getOperandType(action) == ScriptOperandType.Color;
    }

    public Task CapturePixelSearchTopLeftAsync()
    {
        return CapturePixelSearchRegionPointAsync(
            EditorCaptureMode.PixelSearchTopLeft,
            (action, x, y) =>
        {
            var existingRight = action.ScreenLeft + Math.Max(1, action.ScreenWidth) - 1;
            var existingBottom = action.ScreenTop + Math.Max(1, action.ScreenHeight) - 1;
            var previousWidth = Math.Max(1, action.ScreenWidth);
            var previousHeight = Math.Max(1, action.ScreenHeight);

            action.ScreenLeft = x;
            action.ScreenTop = y;
            action.ScreenWidth = existingRight >= x ? existingRight - x + 1 : previousWidth;
            action.ScreenHeight = existingBottom >= y ? existingBottom - y + 1 : previousHeight;
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCapturedRegionTopLeft"), x, y);
        });
    }

    public Task CapturePixelSearchBottomRightAsync()
    {
        return CapturePixelSearchRegionPointAsync(
            EditorCaptureMode.PixelSearchBottomRight,
            (action, x, y) =>
        {
            var width = x - action.ScreenLeft + 1;
            var height = y - action.ScreenTop + 1;
            if (width <= 0 || height <= 0)
            {
                Status = Localize("Editor_StatusCaptureRegionInvalidBottomRight");
                return;
            }

            action.ScreenWidth = width;
            action.ScreenHeight = height;
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCapturedRegionBottomRight"), x, y);
        });
    }

    private async Task CapturePixelSearchRegionPointAsync(EditorCaptureMode mode, Action<EditorAction, int, int> applyPoint)
    {
        var targetAction = SelectedAction;
        if (targetAction == null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (targetAction.Type != EditorActionType.PixelSearch)
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        CaptureMode = mode;
        Status = Localize("Editor_StatusCaptureMousePrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token);

            await RunOnUiThreadAsync(() =>
            {
                if (!result.HasValue)
                {
                    Status = Localize("Editor_StatusCaptureCancelled");
                    return;
                }

                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = Localize("Editor_StatusCaptureSelectionChanged");
                    return;
                }

                applyPoint(targetAction, result.Value.X, result.Value.Y);
            });
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message);
        }
        finally
        {
            CaptureMode = EditorCaptureMode.None;
        }
    }

    public void CancelCapture()
    {
        _captureService.CancelCapture();
        CaptureMode = EditorCaptureMode.None;
        Status = Localize("Editor_StatusCaptureCancelled");
    }

    public async Task SaveMacroAsync()
    {
        if (Actions.Count == 0)
        {
            await _dialogService.ShowMessageAsync(Localize("Editor_DialogTitleNoActions"), Localize("Editor_DialogMessageNoActions"));
            return;
        }

        var normalizedActions = CloneState(Actions);
        NormalizeCurrentPositionMouseButtonActionSnapshot(normalizedActions);

        var (isValid, errors) = _validator.ValidateAll(normalizedActions);
        if (!isValid)
        {
            var errorMessage = $"{Localize("Editor_ValidationErrorHeader")}\n\n{string.Join("\n", errors.Select(error => $"• {error}"))}";
            await _dialogService.ShowMessageAsync(Localize("Editor_DialogTitleValidationErrors"), errorMessage);
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusValidationFailed"), errors.Count);
            return;
        }

        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = Localize("Editor_MacroFileDialogName"), Extensions = new[] { MacroFileExtension.TrimStart('.') } }
            };

            var baseName = MacroName.EndsWith(MacroFileExtension, StringComparison.OrdinalIgnoreCase)
                ? MacroName[..^MacroFileExtension.Length]
                : MacroName;
            var filePath = await _dialogService.ShowSaveFileDialogAsync(Localize("Editor_SaveDialogTitle"), $"{baseName}{MacroFileExtension}", filters);

            if (string.IsNullOrEmpty(filePath))
            {
                Status = Localize("Editor_StatusSaveCancelled");
                return;
            }

            var firstCoordinateAction = normalizedActions.FirstOrDefault(action =>
                UsesCoordinateFields(action.Type) && !IsCurrentPositionMouseButtonAction(action));
            var isAbsolute = firstCoordinateAction?.IsAbsolute ?? false;
            var skipInitialZeroZero = _skipInitialZeroZero || RequiresSkipInitialZeroZero;
            if (_skipInitialZeroZero != skipInitialZeroZero)
            {
                _skipInitialZeroZero = skipInitialZeroZero;
                OnPropertyChanged(nameof(SkipInitialZeroZero));
            }

            var sequence = _converter.ToMacroSequence(normalizedActions, MacroName, isAbsolute, skipInitialZeroZero);
            await _fileManager.SaveAsync(sequence, filePath);

            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusSaved"), Path.GetFileName(filePath));
            MacroCreated?.Invoke(this, new EditorMacroCreatedEventArgs(sequence, filePath));
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusSaveError"), ex.Message);
        }
    }

    public async Task LoadMacroAsync()
    {
        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = Localize("Editor_MacroFileDialogName"), Extensions = new[] { MacroFileExtension.TrimStart('.') } }
            };

            var filePath = await _dialogService.ShowOpenFileDialogAsync(Localize("Editor_LoadDialogTitle"), filters);

            if (string.IsNullOrEmpty(filePath))
            {
                Status = Localize("Editor_StatusLoadCancelled");
                return;
            }

            var sequence = await _fileManager.LoadAsync(filePath);
            if (sequence == null)
            {
                SetLoadWarnings(Array.Empty<EditorActionRestoreWarning>());
                Status = Localize("Editor_StatusLoadFailed");
                return;
            }

            LoadMacroSequence(sequence);
            var baseStatus = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusLoaded"), Path.GetFileName(filePath));
            Status = HasLoadWarnings
                ? string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusLoadedWithWarnings"), Path.GetFileName(filePath), LoadWarnings.Count)
                : baseStatus;
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusLoadError"), ex.Message);
        }
    }

    /// <summary>
    /// Loads a MacroSequence for editing.
    /// </summary>
    public void LoadMacroSequence(MacroSequence sequence)
    {
        SaveUndoState();

        ClearLoadedMacroSessionLink();
        Actions.Clear();
        MacroName = sequence.Name;

        var restoreResult = _converter.FromMacroSequenceWithDiagnostics(sequence);
        var editorActions = restoreResult.Actions;
        SetLoadWarnings(restoreResult.Warnings);
        if (sequence.ScriptSteps.Count > 0 && !restoreResult.RestoredFromScriptSteps)
        {
            LoadWarnings.Add(Localize("Editor_StatusRestoreWarningFallback"));
        }

        foreach (var action in editorActions)
        {
            Actions.Add(action);
        }

        var hasCurrentPositionMouseButtons = editorActions.Any(IsCurrentPositionMouseButtonAction);
        _skipInitialZeroZero = sequence.SkipInitialZeroZero || hasCurrentPositionMouseButtons;
        _skipInitialZeroZeroForcedByCurrentPosition = hasCurrentPositionMouseButtons;
        _skipInitialZeroZeroBeforeCurrentPositionForce = sequence.SkipInitialZeroZero;

        SelectedAction = Actions.FirstOrDefault();
        OnPropertyChanged(nameof(HasActions));
        RefreshCurrentPositionConfiguration();
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }
}
