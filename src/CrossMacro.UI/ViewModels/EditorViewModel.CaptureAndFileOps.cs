using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CrossMacro.Core.Models;
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
            Status = StatusSelectActionFirst;
            return;
        }

        IsCapturing = true;
        Status = StatusCaptureMousePrompt;

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token);

            await RunOnUiThreadAsync(() =>
            {
                if (!result.HasValue)
                {
                    Status = StatusCaptureCancelled;
                    return;
                }

                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = StatusCaptureSelectionChanged;
                    return;
                }

                targetAction.X = result.Value.X;
                targetAction.Y = result.Value.Y;
                Status = $"Captured position: ({result.Value.X}, {result.Value.Y})";
            });
        }
        catch (Exception ex)
        {
            Status = $"Capture error: {ex.Message}";
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
            Status = StatusSelectActionFirst;
            return;
        }

        IsCapturing = true;
        Status = StatusCaptureKeyPrompt;

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureKeyCodeAsync(cancellationTokenSource.Token);

            await RunOnUiThreadAsync(() =>
            {
                if (!result.HasValue)
                {
                    Status = StatusCaptureCancelled;
                    return;
                }

                if (!ReferenceEquals(SelectedAction, targetAction))
                {
                    Status = StatusCaptureSelectionChanged;
                    return;
                }

                targetAction.KeyCode = result.Value;
                targetAction.KeyName = _keyCodeMapper.GetKeyName(result.Value);
                Status = $"Captured: {targetAction.KeyName} (code: {result.Value})";
            });
        }
        catch (Exception ex)
        {
            Status = $"Capture error: {ex.Message}";
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
        Status = StatusCaptureCancelled;
    }

    public async Task SaveMacroAsync()
    {
        if (Actions.Count == 0)
        {
            await _dialogService.ShowMessageAsync(DialogTitleNoActions, DialogMessageNoActions);
            return;
        }

        var (isValid, errors) = _validator.ValidateAll(Actions);
        if (!isValid)
        {
            var errorMessage = $"{ValidationErrorHeader}\n\n{string.Join("\n", errors.Select(error => $"• {error}"))}";
            await _dialogService.ShowMessageAsync(DialogTitleValidationErrors, errorMessage);
            Status = $"Validation failed: {errors.Count} error(s)";
            return;
        }

        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = MacroFileDialogName, Extensions = new[] { MacroFileExtension.TrimStart('.') } }
            };

            var baseName = MacroName.EndsWith(MacroFileExtension, StringComparison.OrdinalIgnoreCase)
                ? MacroName[..^MacroFileExtension.Length]
                : MacroName;
            var filePath = await _dialogService.ShowSaveFileDialogAsync(SaveDialogTitle, $"{baseName}{MacroFileExtension}", filters);

            if (string.IsNullOrEmpty(filePath))
            {
                Status = StatusSaveCancelled;
                return;
            }

            var firstCoordinateAction = Actions.FirstOrDefault(action =>
                UsesCoordinateFields(action.Type) && !IsCurrentPositionClickAction(action));
            var isAbsolute = firstCoordinateAction?.IsAbsolute ?? false;
            var skipInitialZeroZero = _skipInitialZeroZero || RequiresSkipInitialZeroZero;
            if (_skipInitialZeroZero != skipInitialZeroZero)
            {
                _skipInitialZeroZero = skipInitialZeroZero;
                OnPropertyChanged(nameof(SkipInitialZeroZero));
            }

            var sequence = _converter.ToMacroSequence(Actions, MacroName, isAbsolute, skipInitialZeroZero);
            await _fileManager.SaveAsync(sequence, filePath);

            Status = $"Saved: {Path.GetFileName(filePath)}";
            MacroCreated?.Invoke(this, sequence);
        }
        catch (Exception ex)
        {
            Status = $"Save error: {ex.Message}";
        }
    }

    public async Task LoadMacroAsync()
    {
        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = MacroFileDialogName, Extensions = new[] { MacroFileExtension.TrimStart('.') } }
            };

            var filePath = await _dialogService.ShowOpenFileDialogAsync(LoadDialogTitle, filters);

            if (string.IsNullOrEmpty(filePath))
            {
                Status = StatusLoadCancelled;
                return;
            }

            var sequence = await _fileManager.LoadAsync(filePath);
            if (sequence == null)
            {
                Status = StatusLoadFailed;
                return;
            }

            LoadMacroSequence(sequence);
            Status = $"Loaded: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Load error: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads a MacroSequence for editing.
    /// </summary>
    public void LoadMacroSequence(MacroSequence sequence)
    {
        SaveUndoState();

        Actions.Clear();
        MacroName = sequence.Name;

        var editorActions = _converter.FromMacroSequence(sequence);
        foreach (var action in editorActions)
        {
            Actions.Add(action);
        }

        var hasCurrentPositionClicks = editorActions.Any(IsCurrentPositionClickAction);
        _skipInitialZeroZero = sequence.SkipInitialZeroZero || hasCurrentPositionClicks;
        _skipInitialZeroZeroForcedByCurrentPosition = hasCurrentPositionClicks;
        _skipInitialZeroZeroBeforeCurrentPositionForce = sequence.SkipInitialZeroZero;

        SelectedAction = Actions.FirstOrDefault();
        OnPropertyChanged(nameof(HasActions));
        RefreshCurrentPositionConfiguration();
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }
}
