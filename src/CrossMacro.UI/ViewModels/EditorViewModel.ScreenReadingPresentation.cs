using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CrossMacro.Core.Models;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    private WriteableBitmap? _selectedImageAssetPreview;

    public IReadOnlyList<EditorActionScreenTargetColorSource> ScreenTargetColorSources => EditorScreenTargetColorSources;
    public IReadOnlyList<EditorImageMatchMode> ImageMatchModes { get; } = Enum.GetValues<EditorImageMatchMode>();
    public bool ShowPixelColorFields => SelectedAction?.Type == EditorActionType.PixelColor;
    public bool ShowWaitColorFields => SelectedAction?.Type == EditorActionType.WaitColor;
    public bool ShowPixelSearchFields => SelectedAction?.Type == EditorActionType.PixelSearch;
    public bool ShowImageSearchFields => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public WriteableBitmap? SelectedImageAssetPreview => _selectedImageAssetPreview;
    public bool ShowSelectedImageAssetPreview => ShowImageSearchFields && SelectedImageAssetPreview is not null;
    public bool ShowImageOutputVariableFields => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public bool ShowImageWaitTimeoutField => SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage;
    public bool ShowScreenReadingFields => ShowPixelColorFields || ShowWaitColorFields || ShowPixelSearchFields || ShowImageSearchFields;
    public bool ShowScreenReadingColorFields => ShowWaitColorFields || ShowPixelSearchFields;
    public bool ShowScreenReadingPointFields => ShowPixelColorFields || ShowWaitColorFields;
    public IReadOnlyList<string> AvailableColorVariableNames => _availableColorVariableNames;
    public bool HasAvailableColorVariableNames => AvailableColorVariableNames.Count > 0;
    public bool ShowScreenTargetColorHexInput => ShowScreenReadingColorFields
        && SelectedAction?.ScreenTargetColorSource == EditorActionScreenTargetColorSource.ManualHex;
    public bool ShowScreenTargetColorVariableInput => ShowScreenReadingColorFields
        && SelectedAction?.ScreenTargetColorSource == EditorActionScreenTargetColorSource.Variable;
    public bool ShowScreenTargetColorVariablePicker => ShowScreenTargetColorVariableInput && HasAvailableColorVariableNames;
    public bool ShowScreenReadingRawAssistance => SelectedAction?.Type == EditorActionType.RawScriptStep
        && TryGetRawScreenReadingHint(SelectedAction.Text, out _);
    public string ScreenReadingRawHint => SelectedAction?.Type == EditorActionType.RawScriptStep
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

    private void RefreshSelectedImageAssetPreview()
    {
        SetSelectedImageAssetPreview(null);
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

        try
        {
            var previewDecoder = _imageAssetPreviewDecoder
                ?? throw new InvalidOperationException("Image asset preview decoder is not registered.");
            SetSelectedImageAssetPreview(CreatePreviewBitmap(previewDecoder.Decode(encoded, assetName)));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            Status = string.Format(
                _localizationService.CurrentCulture,
                Localize("Editor_StatusImagePreviewError"),
                ex.Message);
        }
    }

    private void SetSelectedImageAssetPreview(WriteableBitmap? preview)
    {
        if (ReferenceEquals(_selectedImageAssetPreview, preview))
        {
            return;
        }

        var previous = _selectedImageAssetPreview;
        _selectedImageAssetPreview = preview;
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
            && payload.ScreenTargetColorSource == EditorActionScreenTargetColorSource.ManualHex)
        {
            return NormalizePreviewColor(payload.ScreenColorHex);
        }

        if (SelectedAction.Type == EditorActionType.RawScriptStep
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
        if (tokens.Length == 0)
        {
            return false;
        }

        hint = tokens[0].ToLowerInvariant() switch
        {
            "window" => Localize("Editor_RawScriptHint_Window"),
            "clipboard" => Localize("Editor_RawScriptHint_Clipboard"),
            "shell" => Localize("Editor_RawScriptHint_Shell"),
            "pixelcolor" => Localize("Editor_RawScreenReadingHint_PixelColor"),
            "waitcolor" => Localize("Editor_RawScreenReadingHint_WaitColor"),
            "pixelsearch" => Localize("Editor_RawScreenReadingHint_PixelSearch"),
            "imagesearch" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "imageclick" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "waitimage" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            _ => string.Empty
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
        if (tokens.Length == 0)
        {
            return false;
        }

        hint = tokens[0].ToLowerInvariant() switch
        {
            "pixelcolor" => Localize("Editor_RawScreenReadingHint_PixelColor"),
            "waitcolor" => Localize("Editor_RawScreenReadingHint_WaitColor"),
            "pixelsearch" => Localize("Editor_RawScreenReadingHint_PixelSearch"),
            "imagesearch" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "imageclick" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            "waitimage" => Localize("Editor_RawScreenReadingHint_ImageSearch"),
            _ => string.Empty
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
        if (color.Length != 6 || color.Any(ch => !Uri.IsHexDigit(ch)))
        {
            return string.Empty;
        }

        return color;
    }
}
