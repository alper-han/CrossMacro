
namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    public async Task CaptureMouseAsync()
    {
        var targetAction = SelectedAction;
        if (targetAction is null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        CaptureMode = EditorCaptureMode.Position;
        Status = Localize("Editor_StatusCaptureMousePrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                if (result is null)
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
                    if (targetAction.Type is EditorActionType.PixelColor)
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
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => CaptureMode = EditorCaptureMode.None).ConfigureAwait(false);
        }
    }

    public async Task CaptureKeyAsync()
    {
        var targetAction = SelectedAction;
        if (targetAction is null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        CaptureMode = EditorCaptureMode.Key;
        Status = Localize("Editor_StatusCaptureKeyPrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureKeyCodeAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                if (result is null)
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
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => CaptureMode = EditorCaptureMode.None).ConfigureAwait(false);
        }
    }

    public async Task CaptureTargetColorAsync()
    {
        var targetAction = SelectedAction;
        if (targetAction is null)
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
            var positionResult = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            if (positionResult is null)
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusCaptureCancelled")).ConfigureAwait(false);
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
            }).ConfigureAwait(false);

            if (selectionChanged)
            {
                return;
            }

            var point = new ScreenPoint(positionResult.Value.X, positionResult.Value.Y);
            var pixelResult = await screenPixelReader.GetPixelAsync(point, new ScreenReadOptions(cancellationToken: cancellationTokenSource.Token)).ConfigureAwait(false);

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
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => CaptureMode = EditorCaptureMode.None).ConfigureAwait(false);
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
        if (targetAction is null)
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
            var positionResult = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            if (positionResult is null)
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusCaptureCancelled")).ConfigureAwait(false);
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
            }).ConfigureAwait(false);

            if (!canReadPixel)
            {
                return;
            }

            var point = new ScreenPoint(positionResult.Value.X, positionResult.Value.Y);
            var pixelResult = await screenPixelReader.GetPixelAsync(point, new ScreenReadOptions(cancellationToken: cancellationTokenSource.Token)).ConfigureAwait(false);

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
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => CaptureMode = EditorCaptureMode.None).ConfigureAwait(false);
        }
    }

    private static bool IsConditionColorTarget(EditorAction action, Func<EditorAction, ScriptOperandType> getOperandType)
    {
        return action.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
&& getOperandType(action) is ScriptOperandType.Color;
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

    public Task CaptureScreenshotRegionStartAsync()
    {
        return CaptureScreenshotRegionPointAsync(
            EditorCaptureMode.ScreenshotRegionStart,
            (action, x, y) =>
        {
            action.ScreenshotUseRegion = true;
            action.ScreenshotRegionX = x.ToString(System.Globalization.CultureInfo.InvariantCulture);
            action.ScreenshotRegionY = y.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (!IsPositiveIntegerOrVariable(action.ScreenshotRegionWidth))
            {
                action.ScreenshotRegionWidth = "1";
            }

            if (!IsPositiveIntegerOrVariable(action.ScreenshotRegionHeight))
            {
                action.ScreenshotRegionHeight = "1";
            }

            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCapturedRegionTopLeft"), x, y);
        });
    }

    public Task CaptureScreenshotRegionEndAsync()
    {
        return CaptureScreenshotRegionPointAsync(
            EditorCaptureMode.ScreenshotRegionEnd,
            (action, endX, endY) =>
        {
            var startX = TryParseInteger(action.ScreenshotRegionX, out var parsedStartX) ? parsedStartX : endX;
            var startY = TryParseInteger(action.ScreenshotRegionY, out var parsedStartY) ? parsedStartY : endY;
            var x = Math.Min(startX, endX);
            var y = Math.Min(startY, endY);
            var widthValue = Math.Abs((long)endX - startX) + 1;
            var heightValue = Math.Abs((long)endY - startY) + 1;
            if (widthValue > int.MaxValue || heightValue > int.MaxValue)
            {
                Status = Localize("Editor_StatusCaptureRegionInvalidBottomRight");
                return;
            }

            var width = (int)Math.Max(1L, widthValue);
            var height = (int)Math.Max(1L, heightValue);

            action.ScreenshotUseRegion = true;
            action.ScreenshotRegionX = x.ToString(System.Globalization.CultureInfo.InvariantCulture);
            action.ScreenshotRegionY = y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            action.ScreenshotRegionWidth = width.ToString(System.Globalization.CultureInfo.InvariantCulture);
            action.ScreenshotRegionHeight = height.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCapturedRegionBottomRight"), endX, endY);
        });
    }

    private async Task CapturePixelSearchRegionPointAsync(EditorCaptureMode mode, Action<EditorAction, int, int> applyPoint)
    {
        var targetAction = SelectedAction;
        if (targetAction is null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (targetAction.Type is not (EditorActionType.PixelSearch or EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage))
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        CaptureMode = mode;
        Status = Localize("Editor_StatusCaptureMousePrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                if (result is null)
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
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => CaptureMode = EditorCaptureMode.None).ConfigureAwait(false);
        }
    }

    private async Task CaptureScreenshotRegionPointAsync(EditorCaptureMode mode, Action<EditorAction, int, int> applyPoint)
    {
        var targetAction = SelectedAction;
        if (targetAction is null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (targetAction.Type is not EditorActionType.Screenshot)
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        CaptureMode = mode;
        Status = Localize("Editor_StatusCaptureMousePrompt");

        try
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureMousePositionAsync(cancellationTokenSource.Token).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                if (result is null)
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
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusCaptureError"), ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() => CaptureMode = EditorCaptureMode.None).ConfigureAwait(false);
        }
    }

    private static bool TryParseInteger(string token, out int value)
    {
        return int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static bool IsPositiveIntegerOrVariable(string token)
    {
        return int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value > 0
            : token.StartsWith('$') && EditorActionScriptTokens.IsValidVariableName(token);
    }

    public void CancelCapture()
    {
        _captureService.CancelCapture();
        CaptureMode = EditorCaptureMode.None;
        Status = Localize("Editor_StatusCaptureCancelled");
    }

    private async Task<MacroSequence?> BuildValidMacroSequenceAsync()
    {
        if (Actions.Count is 0)
        {
            await _dialogService.ShowMessageAsync(Localize("Editor_DialogTitleNoActions"), Localize("Editor_DialogMessageNoActions")).ConfigureAwait(false);
            return null;
        }

        var normalizedActions = CloneState(Actions);
        NormalizeCurrentPositionMouseButtonActionSnapshot(normalizedActions);

        var (isValid, validationErrors) = _validator.ValidateAll(normalizedActions);
        var errors = validationErrors.ToList();
        errors.AddRange(ValidateImageSearchAssets(normalizedActions));
        if (!isValid || errors.Count > 0)
        {
            var errorMessage = $"{Localize("Editor_ValidationErrorHeader")}\n\n{string.Join('\n', errors.Select(error => $"• {error}"))}";
            await _dialogService.ShowMessageAsync(Localize("Editor_DialogTitleValidationErrors"), errorMessage).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusValidationFailed"), errors.Count)).ConfigureAwait(false);
            return null;
        }

        var firstCoordinateAction = normalizedActions.FirstOrDefault(action =>
            UsesCoordinateFields(action.Type) && !IsCurrentPositionMouseButtonAction(action));
        var isAbsolute = firstCoordinateAction?.IsAbsolute ?? false;
        var skipInitialZeroZero = _skipInitialZeroZero || RequiresSkipInitialZeroZero;
        await RunOnUiThreadAsync(() =>
        {
            if (_skipInitialZeroZero != skipInitialZeroZero)
            {
                _skipInitialZeroZero = skipInitialZeroZero;
                OnPropertyChanged(nameof(SkipInitialZeroZero));
            }
        }).ConfigureAwait(false);

        var projection = new EditorMacroProjection(
            normalizedActions,
            MacroName,
            isAbsolute,
            skipInitialZeroZero);
        var sequence = _converter.ToMacroSequence(projection);
        if (sequence is null)
        {
            return null;
        }

        sequence.ReplaceImages(_imageAssets);
        return sequence;
    }

    private IEnumerable<string> ValidateImageSearchAssets(IEnumerable<EditorAction> actions)
    {
        var index = 0;
        foreach (var action in actions)
        {
            index++;
            if (action.Type is not (EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(action.ImageAssetName) || !_imageAssets.ContainsKey(action.ImageAssetName))
            {
                yield return string.Format(CultureInfo.InvariantCulture, "Action {0} ({1}): Image asset '{2}' is not imported.", index, action.Type, action.ImageAssetName);
            }
        }
    }


    private CancellationTokenSource? _testPlaybackCts;

    public async Task ToggleTestPlaybackAsync()
    {
        if (IsRunningTest)
        {
            if (_testPlaybackCts is not null)
            {
                await _testPlaybackCts.CancelAsync().ConfigureAwait(false);
            }
            _macroPlayer.StopPlayback();
            return;
        }

        if (_macroPlayer.IsPlaying)
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        var sequence = await BuildValidMacroSequenceAsync().ConfigureAwait(false);
        if (sequence is null)
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            IsRunningTest = true;
            Status = Localize("Editor_StatusTestRunning");
            _testPlaybackCts = new CancellationTokenSource();
        }).ConfigureAwait(false);
        try
        {
            var options = new CrossMacro.Core.Models.PlaybackOptions { Loop = false, RepeatCount = 1 };
            var testPlaybackCts = _testPlaybackCts ?? throw new InvalidOperationException("Test playback cancellation was not initialized.");
            await _macroPlayer.PlayAsync(sequence, options, testPlaybackCts.Token).ConfigureAwait(false);
            if (!testPlaybackCts.IsCancellationRequested)
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusTestComplete")).ConfigureAwait(false);
            }
            else
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusTestCancelled")).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusTestError"), ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() =>
            {
                IsRunningTest = false;
                _testPlaybackCts?.Dispose();
                _testPlaybackCts = null;
            }).ConfigureAwait(false);
        }
    }

    public async Task SaveMacroAsync()
    {
        var sequence = await BuildValidMacroSequenceAsync().ConfigureAwait(false);
        if (sequence is null)
        {
            return;
        }

        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = Localize("Editor_MacroFileDialogName"), Extensions = [MacroFileExtension.TrimStart('.')] },
            };

            var baseName = MacroName.EndsWith(MacroFileExtension, StringComparison.OrdinalIgnoreCase)
                ? MacroName[..^MacroFileExtension.Length]
                : MacroName;
            var filePath = await _dialogService.ShowSaveFileDialogAsync(Localize("Editor_SaveDialogTitle"), $"{baseName}{MacroFileExtension}", filters).ConfigureAwait(false);

            if (string.IsNullOrEmpty(filePath))
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusSaveCancelled")).ConfigureAwait(false);
                return;
            }

            await _fileManager.SaveAsync(sequence, filePath).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusSaved"), Path.GetFileName(filePath));
                MacroCreated?.Invoke(this, new EditorMacroCreatedEventArgs(sequence, filePath));
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusSaveError"), ex.Message)).ConfigureAwait(false);
        }
    }

    public async Task BrowseScreenshotOutputPathAsync()
    {
        var action = SelectedAction;
        if (action is null)
        {
            Status = Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (action.Type is not EditorActionType.Screenshot)
        {
            Status = Localize("Editor_StatusOperationBlocked");
            return;
        }

        var filters = new[]
        {
            new FileDialogFilter { Name = Localize("Editor_ScreenshotFileDialogName"), Extensions = ["png"] },
        };
        var currentFileName = Path.GetFileName(action.ScreenshotOutputPath);
        var defaultFileName = string.IsNullOrWhiteSpace(currentFileName)
            ? Localize("Editor_ScreenshotDefaultFileName")
            : currentFileName;

        var filePath = await _dialogService.ShowSaveFileDialogAsync(
            Localize("Editor_ScreenshotSaveDialogTitle"),
            defaultFileName,
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            await RunOnUiThreadAsync(() => action.ScreenshotOutputPath = filePath).ConfigureAwait(false);
        }
    }

    public async Task ImportImageAssetAsync()
    {
        var filters = new[]
        {
            new FileDialogFilter { Name = Localize("Editor_ImageAssetFileDialogName"), Extensions = ["png"] },
        };

        var filePath = await _dialogService.ShowOpenFileDialogAsync(Localize("Editor_ImageAssetImportDialogTitle"), filters).ConfigureAwait(false);
        if (string.IsNullOrEmpty(filePath))
        {
            await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusImageImportCancelled")).ConfigureAwait(false);
            return;
        }

        try
        {
            var imageAssetCodec = _imageAssetCodec
                ?? throw new InvalidOperationException("Image asset codec is not registered.");
            var cancellationToken = _viewModelCts.Token;
            using var frame = await imageAssetCodec.DecodeFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            using var encoded = new MemoryStream();
            await imageAssetCodec.EncodePngAsync(frame, encoded, cancellationToken).ConfigureAwait(false);
            var encodedImage = Convert.ToBase64String(encoded.ToArray());
            await RunOnUiThreadAsync(() =>
            {
                var assetName = GenerateUniqueImageAssetName(Path.GetFileNameWithoutExtension(filePath));
                _imageAssets[assetName] = encodedImage;
                ImageAssetNames.Add(assetName);
                OnPropertyChanged(nameof(HasImageAssets));

                if (SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage)
                {
                    SelectedAction.ImageAssetName = assetName;
                }

                Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusImageImported"), assetName);
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusImageImportError"), ex.Message)).ConfigureAwait(false);
        }
    }

    private string GenerateUniqueImageAssetName(string? sourceName)
    {
        var baseName = NormalizeImageAssetName(sourceName);
        var candidate = baseName;
        var suffix = 2;
        while (_imageAssets.ContainsKey(candidate))
        {
            candidate = $"{baseName}_{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }

        return candidate;
    }

    private static string NormalizeImageAssetName(string? sourceName)
    {
        var raw = string.IsNullOrWhiteSpace(sourceName) ? "image" : sourceName.Trim();
        var chars = raw.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var normalized = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "image";
        }

        if (!char.IsAsciiLetter(normalized[0]) && normalized[0] != '_')
        {
            normalized = $"image_{normalized}";
        }

        return EditorActionScriptTokens.IsValidVariableName(normalized) ? normalized : "image";
    }

    public async Task LoadMacroAsync()
    {
        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = Localize("Editor_MacroFileDialogName"), Extensions = [MacroFileExtension.TrimStart('.')] },
            };

            var filePath = await _dialogService.ShowOpenFileDialogAsync(Localize("Editor_LoadDialogTitle"), filters).ConfigureAwait(false);

            if (string.IsNullOrEmpty(filePath))
            {
                await RunOnUiThreadAsync(() => Status = Localize("Editor_StatusLoadCancelled")).ConfigureAwait(false);
                return;
            }

            var sequence = await _fileManager.LoadAsync(filePath).ConfigureAwait(false);
            if (sequence is null)
            {
                await RunOnUiThreadAsync(() =>
                {
                    SetLoadWarnings([]);
                    Status = Localize("Editor_StatusLoadFailed");
                }).ConfigureAwait(false);
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                LoadMacroSequence(sequence);
                var baseStatus = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusLoaded"), Path.GetFileName(filePath));
                Status = HasLoadWarnings
                    ? string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusLoadedWithWarnings"), Path.GetFileName(filePath), LoadWarnings.Count)
                    : baseStatus;
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(_localizationService.CurrentCulture, Localize("Editor_StatusLoadError"), ex.Message)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Loads a MacroSequence for editing.
    /// </summary>
    public void LoadMacroSequence(MacroSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        SaveUndoState();

        ClearLoadedMacroSessionLink();
        SetSelectedImageAssetPreview(preview: null);
        var restoreResult = _converter.FromMacroSequenceWithDiagnostics(sequence);
        var editorActions = restoreResult.Actions;
        SetLoadWarnings(restoreResult.Warnings);
        if (sequence.ScriptSteps.Count > 0 && !restoreResult.RestoredFromScriptSteps)
        {
            LoadWarnings.Add(Localize("Editor_StatusRestoreWarningFallback"));
        }

        _isBatchUpdatingActions = true;
        try
        {
            Actions.Clear();
            _imageAssets.Clear();
            ImageAssetNames.Clear();
            if (sequence.Images is { Count: > 0 })
            {
                foreach (var image in sequence.Images.OrderBy(image => image.Key, StringComparer.Ordinal))
                {
                    _imageAssets[image.Key] = image.Value;
                    ImageAssetNames.Add(image.Key);
                }
            }

            MacroName = sequence.Name;
            foreach (var action in editorActions)
            {
                Actions.Add(action);
            }
        }
        finally
        {
            _isBatchUpdatingActions = false;
        }

        OnPropertyChanged(nameof(HasImageAssets));
        var hasCurrentPositionMouseButtons = editorActions.Any(IsCurrentPositionMouseButtonAction);
        _skipInitialZeroZero = sequence.SkipInitialZeroZero || hasCurrentPositionMouseButtons;
        _skipInitialZeroZeroForcedByCurrentPosition = hasCurrentPositionMouseButtons;
        _skipInitialZeroZeroBeforeCurrentPositionForce = sequence.SkipInitialZeroZero;

        SelectedAction = Actions.FirstOrDefault();
        OnPropertyChanged(nameof(HasActions));
        RefreshActionCollectionState();
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }
}
