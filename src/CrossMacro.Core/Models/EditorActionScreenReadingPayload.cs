
namespace CrossMacro.Core.Models;

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
    public const double DefaultImageSearchSimilarity = 0.95;

    public string ImageAssetName { get; init; } = string.Empty;
    public double ImageSearchSimilarity { get; init; } = DefaultImageSearchSimilarity;
    public EditorImageMatchMode ImageSearchMatchMode { get; init; } = EditorImageMatchMode.Automatic;
    public bool ImageSearchMatchModeWasExplicit { get; init; }
    public MacroMouseButton Button { get; init; } = MacroMouseButton.Left;

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
            ImageSearchMatchMode = action.ImageSearchMatchMode,
            ImageSearchMatchModeWasExplicit = action.ImageSearchMatchModeWasExplicit,
            Button = action.Button,
        };
        return true;
    }

    public static bool TryCreateDefault(EditorActionType type, out EditorActionScreenReadingPayload payload)
    {
        payload = type switch
        {
            EditorActionType.PixelColor => ForPixelColor(isAbsolute: true, 0, 0, DefaultColorVariableName),
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
            EditorActionType.MouseMove
                or EditorActionType.MouseClick
                or EditorActionType.MouseDown
                or EditorActionType.MouseUp
                or EditorActionType.KeyPress
                or EditorActionType.KeyDown
                or EditorActionType.KeyUp
                or EditorActionType.Delay
                or EditorActionType.ScrollVertical
                or EditorActionType.ScrollHorizontal
                or EditorActionType.TextInput
                or EditorActionType.SetVariable
                or EditorActionType.IncrementVariable
                or EditorActionType.DecrementVariable
                or EditorActionType.MultiplyVariable
                or EditorActionType.DivideVariable
                or EditorActionType.RepeatBlockStart
                or EditorActionType.IfBlockStart
                or EditorActionType.ElseBlockStart
                or EditorActionType.WhileBlockStart
                or EditorActionType.ForBlockStart
                or EditorActionType.BlockEnd
                or EditorActionType.Break
                or EditorActionType.Continue
                or EditorActionType.ClipboardGet
                or EditorActionType.ClipboardSet
                or EditorActionType.ShellCommand
                or EditorActionType.Screenshot
                or EditorActionType.WindowCommand
                or EditorActionType.RawScriptStep => default,
            _ => default,
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
IsAbsolute: true,
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
IsAbsolute: true,
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
IsAbsolute: true,
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
            Button = MacroMouseButton.Left,
        };
    }

    public IEnumerable<string> OutputVariableNames => GetOutputVariableNames();

    private IEnumerable<string> GetOutputVariableNames()
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
            EditorActionType.MouseMove or EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp or EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp or EditorActionType.Delay or EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal or EditorActionType.TextInput or EditorActionType.SetVariable or EditorActionType.IncrementVariable or EditorActionType.DecrementVariable or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable or EditorActionType.RepeatBlockStart or EditorActionType.IfBlockStart or EditorActionType.ElseBlockStart or EditorActionType.WhileBlockStart or EditorActionType.ForBlockStart or EditorActionType.BlockEnd or EditorActionType.Break or EditorActionType.Continue or EditorActionType.ClipboardGet or EditorActionType.ClipboardSet or EditorActionType.ShellCommand or EditorActionType.Screenshot or EditorActionType.WindowCommand or EditorActionType.RawScriptStep => EditorActionScreenReadingVariableRole.None,
            _ => EditorActionScreenReadingVariableRole.None,
        };
    }

    public bool HasValidRgbColor()
    {
        if (ScreenColorHex.Length is not 6)
        {
            return false;
        }

        return ScreenColorHex.All(Uri.IsHexDigit);
    }

    public bool HasValidTargetColor()
    {
        return ScreenTargetColorSource switch
        {
            EditorActionScreenTargetColorSource.Variable => HasValidTargetColorVariableName(),
            EditorActionScreenTargetColorSource.ManualHex => HasValidRgbColor(),
            _ => HasValidRgbColor(),
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
        return ScreenTargetColorSource is EditorActionScreenTargetColorSource.Variable
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
