
namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    public static IReadOnlyList<EditorActionScreenTargetColorSource> ScreenTargetColorSources { get; } =
        [
            EditorActionScreenTargetColorSource.ManualHex,
            EditorActionScreenTargetColorSource.Variable,
        ];
    public IReadOnlyList<EditorImageMatchMode> ImageMatchModes { get; } = Enum.GetValues<EditorImageMatchMode>();
    public bool ShowPixelColorFields => (SelectedAction?.Type) is EditorActionType.PixelColor;
    public bool ShowWaitColorFields => (SelectedAction?.Type) is EditorActionType.WaitColor;
    public bool ShowPixelSearchFields => (SelectedAction?.Type) is EditorActionType.PixelSearch;
    public bool ShowImageSearchFields => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public WriteableBitmap? SelectedImageAssetPreview { get; private set; }
    public bool ShowSelectedImageAssetPreview => ShowImageSearchFields && SelectedImageAssetPreview is not null;
    public bool ShowImageOutputVariableFields => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public bool ShowImageWaitTimeoutField => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public bool ShowScreenReadingFields => ShowPixelColorFields || ShowWaitColorFields || ShowPixelSearchFields || ShowImageSearchFields;
    public bool ShowScreenReadingColorFields => ShowWaitColorFields || ShowPixelSearchFields;
    public bool ShowScreenReadingPointFields => ShowPixelColorFields || ShowWaitColorFields;
    public IReadOnlyList<string> AvailableColorVariableNames { get; private set; } = [];
    public bool HasAvailableColorVariableNames => AvailableColorVariableNames.Count > 0;
    public bool ShowScreenTargetColorHexInput => ShowScreenReadingColorFields
&& (SelectedAction?.ScreenTargetColorSource) is EditorActionScreenTargetColorSource.ManualHex;
    public bool ShowScreenTargetColorVariableInput => ShowScreenReadingColorFields
&& (SelectedAction?.ScreenTargetColorSource) is EditorActionScreenTargetColorSource.Variable;
    public bool ShowScreenTargetColorVariablePicker => ShowScreenTargetColorVariableInput && HasAvailableColorVariableNames;
    public bool ShowScreenReadingRawAssistance => (SelectedAction?.Type) is EditorActionType.RawScriptStep
&& TryGetRawScreenReadingHint(SelectedAction.Text, out _);
    public string ScreenReadingRawHint => (SelectedAction?.Type) is EditorActionType.RawScriptStep
&& TryGetRawScreenReadingHint(SelectedAction.Text, out var hint)
            ? hint
            : string.Empty;
    public bool ShowScreenReadingColorPreview => !string.IsNullOrWhiteSpace(ScreenReadingColorPreviewHex);
    public string ScreenReadingColorPreviewHex => GetScreenReadingColorPreviewHex();

    public string? SelectedScreenTargetColorVariableSuggestion
    {
        get => _selectedScreenTargetColorVariableSuggestion;
        set => ApplyVariableSuggestion(
            ref _selectedScreenTargetColorVariableSuggestion,
            value,
            nameof(SelectedScreenTargetColorVariableSuggestion),
            suggestion =>
            {
                if (SelectedAction?.Type is EditorActionType.WaitColor or EditorActionType.PixelSearch)
                {
                    SelectedAction.ScreenTargetColorVariableName = suggestion;
                }
            });
    }

    private void NotifyScreenReadingComputedPropertiesChanged()
    {
        OnPropertyChanged(nameof(TextInputHint));
        OnPropertyChanged(nameof(ShowScreenReadingRawAssistance));
        OnPropertyChanged(nameof(ScreenReadingRawHint));
        OnPropertyChanged(nameof(ShowScreenReadingFields));
        OnPropertyChanged(nameof(ShowImageSearchFields));
        OnPropertyChanged(nameof(ShowImageOutputVariableFields));
        OnPropertyChanged(nameof(ShowImageWaitTimeoutField));
        OnPropertyChanged(nameof(SelectedImageAssetPreview));
        OnPropertyChanged(nameof(ShowSelectedImageAssetPreview));
        OnPropertyChanged(nameof(ShowScreenTargetColorHexInput));
        OnPropertyChanged(nameof(ShowScreenTargetColorVariableInput));
        OnPropertyChanged(nameof(ShowScreenTargetColorVariablePicker));
        OnPropertyChanged(nameof(SelectedScreenTargetColorVariableSuggestion));
        OnPropertyChanged(nameof(ShowScreenReadingColorPreview));
        OnPropertyChanged(nameof(ScreenReadingColorPreviewHex));
    }

    private async Task RefreshSelectedImageAssetPreviewAsync()
    {
        SetSelectedImageAssetPreview(preview: null);
        if (!ShowImageSearchFields)
        {
            return;
        }

        var assetName = SelectedAction?.ImageAssetName;
        if (string.IsNullOrWhiteSpace(assetName)
            || !_imageAssets.TryGetValue(assetName, out var encoded)
            || string.IsNullOrWhiteSpace(encoded))
        {
            Status = string.Format(
                _localizationService.CurrentCulture,
                Localize("Editor_StatusImagePreviewError"),
                assetName ?? Localize("Editor_ImageAsset"));
            return;
        }

        var refreshVersion = Interlocked.Increment(ref _imageAssetPreviewRefreshVersion);
        try
        {
            var previewDecoder = _imageAssetPreviewDecoder
                ?? throw new InvalidOperationException("Image asset preview decoder is not registered.");
            var decoded = await previewDecoder.DecodeAsync(encoded, assetName, _viewModelCts.Token).ConfigureAwait(false);

            // Marshal back to the UI thread; the version check drops stale results when the
            // selection changes quickly.
            await RunOnUiThreadAsync(() =>
            {
                if (refreshVersion == Volatile.Read(ref _imageAssetPreviewRefreshVersion))
                {
                    SetSelectedImageAssetPreview(CreatePreviewBitmap(decoded));
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ViewModel kapanırken beklenen iptal.
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            await RunOnUiThreadAsync(() => Status = string.Format(
                _localizationService.CurrentCulture,
                Localize("Editor_StatusImagePreviewError"),
                ex.Message)).ConfigureAwait(false);
        }
    }

    private int _imageAssetPreviewRefreshVersion;

    private void SetSelectedImageAssetPreview(WriteableBitmap? preview)
    {
        if (ReferenceEquals(SelectedImageAssetPreview, preview))
        {
            return;
        }

        var previous = SelectedImageAssetPreview;
        SelectedImageAssetPreview = preview;
        previous?.Dispose();
        OnPropertyChanged(nameof(SelectedImageAssetPreview));
        OnPropertyChanged(nameof(ShowSelectedImageAssetPreview));
    }

    private static WriteableBitmap CreatePreviewBitmap(ImageAssetPreview preview)
    {
        var pixels = preview.Pixels.ToArray();
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new WriteableBitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque,
                handle.AddrOfPinnedObject(),
                new PixelSize(preview.Width, preview.Height),
                new Vector(96, 96),
                preview.Stride);
        }
        finally
        {
            handle.Free();
        }
    }

    private string GetScreenReadingColorPreviewHex()
    {
        if (SelectedAction is null)
        {
            return string.Empty;
        }

        if (SelectedAction.TryGetScreenReadingPayload(out var payload)
&& payload.UsesTargetColor
&& payload.ScreenTargetColorSource is EditorActionScreenTargetColorSource.ManualHex)
        {
            return NormalizePreviewColor(payload.ScreenColorHex);
        }

        if (SelectedAction.Type is EditorActionType.RawScriptStep
&& TryExtractRawScreenReadingColor(SelectedAction.Text, out var colorHex))
        {
            return colorHex;
        }

        return string.Empty;
    }

    private bool TryGetRawScriptHint(string? step, out string hint)
    {
        hint = string.Empty;
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is 0)
        {
            return false;
        }

        hint = tokens[0].ToUpperInvariant() switch
        {
            "WINDOW" => Localize("Editor_RawScriptHint_Window"),
            "CLIPBOARD" => Localize("Editor_RawScriptHint_Clipboard"),
            "SHELL" => Localize("Editor_RawScriptHint_Shell"),
            "PIXELCOLOR" => Localize("Editor_RawScreenReadingHint_PixelColor"),
            "WAITCOLOR" => Localize("Editor_RawScreenReadingHint_WaitColor"),
            "PIXELSEARCH" => Localize("Editor_RawScreenReadingHint_PixelSearch"),
            "IMAGESEARCH" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "IMAGECLICK" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "WAITIMAGE" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            _ => string.Empty,
        };

        return hint.Length > 0;
    }

    private bool TryGetRawScreenReadingHint(string? step, out string hint)
    {
        hint = string.Empty;
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is 0)
        {
            return false;
        }

        hint = tokens[0].ToUpperInvariant() switch
        {
            "PIXELCOLOR" => Localize("Editor_RawScreenReadingHint_PixelColor"),
            "WAITCOLOR" => Localize("Editor_RawScreenReadingHint_WaitColor"),
            "PIXELSEARCH" => Localize("Editor_RawScreenReadingHint_PixelSearch"),
            "IMAGESEARCH" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "IMAGECLICK" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "WAITIMAGE" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            _ => string.Empty,
        };

        return hint.Length > 0;
    }

    private static bool TryExtractRawScreenReadingColor(string? step, out string colorHex)
    {
        colorHex = string.Empty;
        if (string.IsNullOrWhiteSpace(step))
        {
            return false;
        }

        var tokens = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length >= 4 && tokens[0].Equals("waitcolor", StringComparison.OrdinalIgnoreCase))
        {
            colorHex = NormalizePreviewColor(tokens[3]);
            return colorHex.Length > 0;
        }

        if (tokens.Length >= 6 && tokens[0].Equals("pixelsearch", StringComparison.OrdinalIgnoreCase))
        {
            colorHex = NormalizePreviewColor(tokens[5]);
            return colorHex.Length > 0;
        }

        return false;
    }

    private static string NormalizePreviewColor(string? value)
    {
        var color = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (color.Length is not 6 || color.Any(ch => !Uri.IsHexDigit(ch)))
        {
            return string.Empty;
        }

        return color;
    }
}
