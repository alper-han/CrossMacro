using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
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

        IsCapturing = true;
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

                targetAction.X = result.Value.X;
                targetAction.Y = result.Value.Y;
                Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCapturedPosition"), result.Value.X, result.Value.Y);
            });
        }
        catch (Exception ex)
        {
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message);
        }
        finally
        {
            IsCapturing = false;
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

        IsCapturing = true;
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
            IsCapturing = false;
        }
    }

    public void CancelCapture()
    {
        _captureService.CancelCapture();
        IsCapturing = false;
        Status = Localize("Editor_StatusCaptureCancelled");
    }

    public async Task SaveMacroAsync()
    {
        if (Actions.Count == 0)
        {
            await _dialogService.ShowMessageAsync(Localize("Editor_DialogTitleNoActions"), Localize("Editor_DialogMessageNoActions"));
            return;
        }

        var (isValid, errors) = _validator.ValidateAll(Actions);
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

            var firstCoordinateAction = Actions.FirstOrDefault(action =>
                UsesCoordinateFields(action.Type) && !IsCurrentPositionMouseButtonAction(action));
            var isAbsolute = firstCoordinateAction?.IsAbsolute ?? false;
            var skipInitialZeroZero = _skipInitialZeroZero || RequiresSkipInitialZeroZero;
            if (_skipInitialZeroZero != skipInitialZeroZero)
            {
                _skipInitialZeroZero = skipInitialZeroZero;
                OnPropertyChanged(nameof(SkipInitialZeroZero));
            }

            var sequence = _converter.ToMacroSequence(Actions, MacroName, isAbsolute, skipInitialZeroZero);
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
