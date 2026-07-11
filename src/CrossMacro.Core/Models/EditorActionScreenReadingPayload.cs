using System;
using System.Collections.Generic;
using CrossMacro.Core.Services;

namespace CrossMacro.Core.Models;

public enum EditorActionScreenReadingVariableRole
{
    None,
    Color,
    Boolean,
    Number
}

public enum EditorActionScreenTargetColorSource
{
    ManualHex = 0,
    Manual = ManualHex,
    Variable = 1
}

public readonly record struct EditorActionScreenReadingPayload(
    EditorActionType Type,
    bool IsAbsolute,
    int ScreenX,
    int ScreenY,
    int ScreenLeft,
    int ScreenTop,
    int ScreenWidth,
    int ScreenHeight,
    string ScreenColorHex,
    EditorActionScreenTargetColorSource ScreenTargetColorSource,
    string ScreenTargetColorVariableName,
    string ScreenColorVariableName,
    int ScreenTimeoutMs,
    int ScreenTolerance,
    string ScreenFoundVariableName,
    string ScreenFoundXVariableName,
    string ScreenFoundYVariableName)
{
    public const string DefaultColorHex = "FFFFFF";
    public const string DefaultColorVariableName = "color";
    public const string DefaultTargetColorVariableName = DefaultColorVariableName;
    public const string DefaultWaitColorVariableName = "wait_ok";
    public const string DefaultFoundVariableName = "found";
    public const string DefaultFoundXVariableName = "found_x";
    public const string DefaultFoundYVariableName = "found_y";
    public const int DefaultTimeoutMs = 5000;
    public const int DefaultTolerance = 0;
    public const int DefaultPointScreenWidth = 1;
    public const int DefaultPointScreenHeight = 1;
    public const int DefaultSearchScreenWidth = 1920;
    public const int DefaultSearchScreenHeight = 1080;
    public const double DefaultImageSearchSimilarity = 1.0;
    public const int DefaultImageSearchDownsample = 1;
    public const bool DefaultImageSearchScaleAware = false;

    public string ImageAssetName { get; init; } = string.Empty;
    public double ImageSearchSimilarity { get; init; } = DefaultImageSearchSimilarity;
    public int ImageSearchDownsample { get; init; } = DefaultImageSearchDownsample;
    public bool ImageSearchScaleAware { get; init; } = DefaultImageSearchScaleAware;
    public EditorImageMatchMode ImageSearchMatchMode { get; init; } = EditorImageMatchMode.FirstThresholdMatch;
    public bool ImageSearchMatchModeWasExplicit { get; init; }
    public MouseButton Button { get; init; } = MouseButton.Left;

    public int ScreenRight => checked(ScreenLeft + ScreenWidth);

    public int ScreenBottom => checked(ScreenTop + ScreenHeight);

    public bool UsesTargetColor => Type is EditorActionType.WaitColor or EditorActionType.PixelSearch;

    public static bool IsScreenReadingAction(EditorActionType type)
    {
        return type is EditorActionType.PixelColor
            or EditorActionType.WaitColor
            or EditorActionType.PixelSearch
            or EditorActionType.ImageSearch
            or EditorActionType.ImageClick
            or EditorActionType.WaitImage;
    }

    public static bool TryCreate(EditorAction action, out EditorActionScreenReadingPayload payload)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!IsScreenReadingAction(action.Type))
        {
            payload = default;
            return false;
        }

        payload = new EditorActionScreenReadingPayload(
            action.Type,
            action.IsAbsolute,
            action.ScreenX,
            action.ScreenY,
            action.ScreenLeft,
            action.ScreenTop,
            action.ScreenWidth,
            action.ScreenHeight,
            action.ScreenColorHex,
            action.ScreenTargetColorSource,
            action.ScreenTargetColorVariableName,
            action.ScreenColorVariableName,
            action.ScreenTimeoutMs,
            action.ScreenTolerance,
            action.ScreenFoundVariableName,
            action.ScreenFoundXVariableName,
            action.ScreenFoundYVariableName)
        {
            ImageAssetName = action.ImageAssetName,
            ImageSearchSimilarity = action.ImageSearchSimilarity,
            ImageSearchDownsample = action.ImageSearchDownsample,
            ImageSearchScaleAware = action.ImageSearchScaleAware,
            ImageSearchMatchMode = action.ImageSearchMatchMode,
            ImageSearchMatchModeWasExplicit = action.ImageSearchMatchModeWasExplicit,
            Button = action.Button
        };
        return true;
    }

    public static bool TryCreateDefault(EditorActionType type, out EditorActionScreenReadingPayload payload)
    {
        payload = type switch
        {
            EditorActionType.PixelColor => ForPixelColor(true, 0, 0, DefaultColorVariableName),
            EditorActionType.WaitColor => ForWaitColor(0, 0, DefaultColorHex, DefaultTimeoutMs, DefaultWaitColorVariableName),
            EditorActionType.PixelSearch => ForPixelSearch(
                0,
                0,
                DefaultSearchScreenWidth,
                DefaultSearchScreenHeight,
                DefaultColorHex,
                DefaultFoundVariableName,
                DefaultFoundXVariableName,
                DefaultFoundYVariableName,
                DefaultTolerance),
            EditorActionType.ImageSearch => ForImageSearch(),
            EditorActionType.ImageClick => ForImageClick(),
            EditorActionType.WaitImage => ForWaitImage(),
            _ => default
        };

        return IsScreenReadingAction(type);
    }

    public static EditorActionScreenReadingPayload ForPixelColor(
        bool isAbsolute,
        int screenX,
        int screenY,
        string colorVariableName)
    {
        return new EditorActionScreenReadingPayload(
            EditorActionType.PixelColor,
            isAbsolute,
            screenX,
            screenY,
            0,
            0,
            DefaultPointScreenWidth,
            DefaultPointScreenHeight,
            DefaultColorHex,
            EditorActionScreenTargetColorSource.ManualHex,
            DefaultTargetColorVariableName,
            colorVariableName,
            DefaultTimeoutMs,
            DefaultTolerance,
            DefaultFoundVariableName,
            DefaultFoundXVariableName,
            DefaultFoundYVariableName);
    }

    public static EditorActionScreenReadingPayload ForWaitColor(
        int screenX,
        int screenY,
        string colorHex,
        int timeoutMs,
        string resultVariableName)
    {
        return new EditorActionScreenReadingPayload(
            EditorActionType.WaitColor,
            true,
            screenX,
            screenY,
            0,
            0,
            DefaultPointScreenWidth,
            DefaultPointScreenHeight,
            colorHex,
            EditorActionScreenTargetColorSource.ManualHex,
            DefaultTargetColorVariableName,
            resultVariableName,
            timeoutMs,
            DefaultTolerance,
            DefaultFoundVariableName,
            DefaultFoundXVariableName,
            DefaultFoundYVariableName);
    }

    public static EditorActionScreenReadingPayload ForPixelSearch(
        int screenLeft,
        int screenTop,
        int screenWidth,
        int screenHeight,
        string colorHex,
        string foundVariableName,
        string foundXVariableName,
        string foundYVariableName,
        int tolerance)
    {
        return new EditorActionScreenReadingPayload(
            EditorActionType.PixelSearch,
            true,
            0,
            0,
            screenLeft,
            screenTop,
            screenWidth,
            screenHeight,
            colorHex,
            EditorActionScreenTargetColorSource.ManualHex,
            DefaultTargetColorVariableName,
            DefaultColorVariableName,
            DefaultTimeoutMs,
            tolerance,
            foundVariableName,
            foundXVariableName,
            foundYVariableName);
    }

    public static EditorActionScreenReadingPayload ForImageSearch() => ForImageAction(EditorActionType.ImageSearch);

    public static EditorActionScreenReadingPayload ForImageClick() => ForImageAction(EditorActionType.ImageClick);

    public static EditorActionScreenReadingPayload ForWaitImage() => ForImageAction(EditorActionType.WaitImage);

    private static EditorActionScreenReadingPayload ForImageAction(EditorActionType type)
    {
        return new EditorActionScreenReadingPayload(
            type,
            true,
            0,
            0,
            0,
            0,
            DefaultSearchScreenWidth,
            DefaultSearchScreenHeight,
            DefaultColorHex,
            EditorActionScreenTargetColorSource.ManualHex,
            DefaultTargetColorVariableName,
            DefaultColorVariableName,
            DefaultTimeoutMs,
            DefaultTolerance,
            DefaultFoundVariableName,
            DefaultFoundXVariableName,
            DefaultFoundYVariableName)
        {
            ImageSearchSimilarity = DefaultImageSearchSimilarity,
            ImageSearchDownsample = DefaultImageSearchDownsample,
            ImageSearchScaleAware = DefaultImageSearchScaleAware,
            Button = MouseButton.Left
        };
    }

    public IEnumerable<string> GetOutputVariableNames()
    {
        switch (Type)
        {
            case EditorActionType.PixelColor:
            case EditorActionType.WaitColor:
                yield return ScreenColorVariableName;
                break;
            case EditorActionType.PixelSearch:
            case EditorActionType.ImageSearch:
            case EditorActionType.ImageClick:
            case EditorActionType.WaitImage:
                yield return ScreenFoundVariableName;
                yield return ScreenFoundXVariableName;
                yield return ScreenFoundYVariableName;
                break;
        }
    }

    public EditorActionScreenReadingVariableRole GetOutputVariableRole(string variableName)
    {
        return Type switch
        {
            EditorActionType.PixelColor when string.Equals(ScreenColorVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Color,
            EditorActionType.WaitColor when string.Equals(ScreenColorVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Boolean,
            EditorActionType.PixelSearch when string.Equals(ScreenFoundVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Boolean,
            EditorActionType.ImageSearch when string.Equals(ScreenFoundVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Boolean,
            EditorActionType.ImageClick when string.Equals(ScreenFoundVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Boolean,
            EditorActionType.WaitImage when string.Equals(ScreenFoundVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Boolean,
            EditorActionType.PixelSearch when string.Equals(ScreenFoundXVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            EditorActionType.ImageSearch when string.Equals(ScreenFoundXVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            EditorActionType.ImageClick when string.Equals(ScreenFoundXVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            EditorActionType.WaitImage when string.Equals(ScreenFoundXVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            EditorActionType.PixelSearch when string.Equals(ScreenFoundYVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            EditorActionType.ImageSearch when string.Equals(ScreenFoundYVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            EditorActionType.ImageClick when string.Equals(ScreenFoundYVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            EditorActionType.WaitImage when string.Equals(ScreenFoundYVariableName, variableName, StringComparison.Ordinal) =>
                EditorActionScreenReadingVariableRole.Number,
            _ => EditorActionScreenReadingVariableRole.None
        };
    }

    public bool HasValidRgbColor()
    {
        if (ScreenColorHex.Length != 6)
        {
            return false;
        }

        foreach (var ch in ScreenColorHex)
        {
            if (!Uri.IsHexDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public bool HasValidTargetColor()
    {
        return ScreenTargetColorSource switch
        {
            EditorActionScreenTargetColorSource.Variable => HasValidTargetColorVariableName(),
            _ => HasValidRgbColor()
        };
    }

    public bool HasPositiveSearchRegion()
    {
        return ScreenWidth > 0 && ScreenHeight > 0;
    }

    public bool HasValidTolerance()
    {
        return ScreenTolerance is >= 0 and <= byte.MaxValue;
    }

    public bool HasValidColorVariableName()
    {
        return EditorActionScriptTokens.IsValidVariableName(ScreenColorVariableName);
    }

    public bool HasValidTargetColorVariableName()
    {
        return EditorActionScriptTokens.IsValidVariableName(ScreenTargetColorVariableName);
    }

    public bool HasValidFoundVariableName()
    {
        return EditorActionScriptTokens.IsValidVariableName(ScreenFoundVariableName);
    }

    public bool HasValidFoundCoordinateVariableNames()
    {
        return EditorActionScriptTokens.IsValidVariableName(ScreenFoundXVariableName)
            && EditorActionScriptTokens.IsValidVariableName(ScreenFoundYVariableName);
    }

    public string NormalizeColorVariableToken()
    {
        return EditorActionScriptTokens.NormalizeVariableToken(ScreenColorVariableName);
    }

    public string NormalizeTargetColorVariableToken()
    {
        return EditorActionScriptTokens.NormalizeVariableToken(ScreenTargetColorVariableName);
    }

    public string FormatTargetColorToken()
    {
        return ScreenTargetColorSource == EditorActionScreenTargetColorSource.Variable
            ? $"${NormalizeTargetColorVariableToken()}"
            : ScreenColorHex.Trim().ToUpperInvariant();
    }

    public string NormalizeFoundVariableToken()
    {
        return EditorActionScriptTokens.NormalizeVariableToken(ScreenFoundVariableName);
    }

    public string NormalizeFoundXVariableToken()
    {
        return EditorActionScriptTokens.NormalizeVariableToken(ScreenFoundXVariableName);
    }

    public string NormalizeFoundYVariableToken()
    {
        return EditorActionScriptTokens.NormalizeVariableToken(ScreenFoundYVariableName);
    }
}
