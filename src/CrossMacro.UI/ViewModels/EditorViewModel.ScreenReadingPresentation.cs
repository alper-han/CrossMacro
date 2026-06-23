using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public partial class EditorViewModel
{
    public IReadOnlyList<EditorActionScreenTargetColorSource> ScreenTargetColorSources => EditorScreenTargetColorSources;
    public bool ShowPixelColorFields => SelectedAction?.Type == EditorActionType.PixelColor;
    public bool ShowWaitColorFields => SelectedAction?.Type == EditorActionType.WaitColor;
    public bool ShowPixelSearchFields => SelectedAction?.Type == EditorActionType.PixelSearch;
    public bool ShowScreenReadingFields => ShowPixelColorFields || ShowWaitColorFields || ShowPixelSearchFields;
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
        OnPropertyChanged(nameof(ShowScreenReadingRawAssistance));
        OnPropertyChanged(nameof(ScreenReadingRawHint));
        OnPropertyChanged(nameof(ShowScreenReadingFields));
        OnPropertyChanged(nameof(ShowScreenTargetColorHexInput));
        OnPropertyChanged(nameof(ShowScreenTargetColorVariableInput));
        OnPropertyChanged(nameof(ShowScreenTargetColorVariablePicker));
        OnPropertyChanged(nameof(SelectedScreenTargetColorVariableSuggestion));
        OnPropertyChanged(nameof(ShowScreenReadingColorPreview));
        OnPropertyChanged(nameof(ScreenReadingColorPreviewHex));
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
