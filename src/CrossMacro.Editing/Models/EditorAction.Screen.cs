namespace CrossMacro.Editing.Models;

public partial class EditorAction
{
    public int ScreenX
    {
        get => _screen.ScreenX;
        set => SetScriptField(ref _screen.ScreenX, value);
    }

    public int ScreenY
    {
        get => _screen.ScreenY;
        set => SetScriptField(ref _screen.ScreenY, value);
    }

    public int ScreenLeft
    {
        get => _screen.ScreenLeft;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenLeft,
            ref _screen.ImageSearchRegionLeftToken,
            nameof(ScreenLeft),
            nameof(ImageSearchRegionLeftToken));
    }

    public int ScreenTop
    {
        get => _screen.ScreenTop;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenTop,
            ref _screen.ImageSearchRegionTopToken,
            nameof(ScreenTop),
            nameof(ImageSearchRegionTopToken));
    }

    public int ScreenWidth
    {
        get => _screen.ScreenWidth;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenWidth,
            ref _screen.ImageSearchRegionWidthToken,
            nameof(ScreenWidth),
            nameof(ImageSearchRegionWidthToken));
    }

    public int ScreenHeight
    {
        get => _screen.ScreenHeight;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenHeight,
            ref _screen.ImageSearchRegionHeightToken,
            nameof(ScreenHeight),
            nameof(ImageSearchRegionHeightToken));
    }

    /// <summary>
    /// Left edge token for image search actions. Accepts an integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionLeftToken
    {
        get => _screen.ImageSearchRegionLeftToken ?? ScreenLeft.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionLeftToken,
            ref _screen.ScreenLeft,
            nameof(ImageSearchRegionLeftToken),
            nameof(ScreenLeft));
    }

    /// <summary>
    /// Top edge token for image search actions. Accepts an integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionTopToken
    {
        get => _screen.ImageSearchRegionTopToken ?? ScreenTop.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionTopToken,
            ref _screen.ScreenTop,
            nameof(ImageSearchRegionTopToken),
            nameof(ScreenTop));
    }

    /// <summary>
    /// Width token for image search actions. Accepts a positive integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionWidthToken
    {
        get => _screen.ImageSearchRegionWidthToken ?? ScreenWidth.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionWidthToken,
            ref _screen.ScreenWidth,
            nameof(ImageSearchRegionWidthToken),
            nameof(ScreenWidth));
    }

    /// <summary>
    /// Height token for image search actions. Accepts a positive integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionHeightToken
    {
        get => _screen.ImageSearchRegionHeightToken ?? ScreenHeight.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionHeightToken,
            ref _screen.ScreenHeight,
            nameof(ImageSearchRegionHeightToken),
            nameof(ScreenHeight));
    }

    public bool TryGetLiteralImageSearchRegion(out int left, out int top, out int width, out int height)
    {
        var hasLeft = int.TryParse(ImageSearchRegionLeftToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out left);
        var hasTop = int.TryParse(ImageSearchRegionTopToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out top);
        var hasWidth = int.TryParse(ImageSearchRegionWidthToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
        var hasHeight = int.TryParse(ImageSearchRegionHeightToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
        return hasLeft && hasTop && hasWidth && hasHeight;
    }

    public bool HasValidImageSearchRegionTokens()
    {
        return IsValidImageSearchRegionToken(ImageSearchRegionLeftToken, mustBePositive: false)
            && IsValidImageSearchRegionToken(ImageSearchRegionTopToken, mustBePositive: false)
            && IsValidImageSearchRegionToken(ImageSearchRegionWidthToken, mustBePositive: true)
            && IsValidImageSearchRegionToken(ImageSearchRegionHeightToken, mustBePositive: true);
    }

    public string ScreenColorHex
    {
        get => _screen.ScreenColorHex;
        set => SetScriptField(ref _screen.ScreenColorHex, NormalizeColorHex(value));
    }

    public EditorActionScreenTargetColorSource ScreenTargetColorSource
    {
        get => _screen.ScreenTargetColorSource;
        set => SetScriptField(ref _screen.ScreenTargetColorSource, value);
    }

    public string ScreenTargetColorVariableName
    {
        get => _screen.ScreenTargetColorVariableName;
        set => SetScriptField(ref _screen.ScreenTargetColorVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenColorVariableName
    {
        get => _screen.ScreenColorVariableName;
        set => SetScriptField(ref _screen.ScreenColorVariableName, value?.Trim() ?? string.Empty);
    }

    public int ScreenTimeoutMs
    {
        get => _screen.ScreenTimeoutMs;
        set => SetScriptField(ref _screen.ScreenTimeoutMs, value);
    }

    public int ScreenTolerance
    {
        get => _screen.ScreenTolerance;
        set => SetScriptField(ref _screen.ScreenTolerance, value);
    }

    public string ScreenFoundVariableName
    {
        get => _screen.ScreenFoundVariableName;
        set => SetScriptField(ref _screen.ScreenFoundVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundXVariableName
    {
        get => _screen.ScreenFoundXVariableName;
        set => SetScriptField(ref _screen.ScreenFoundXVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundYVariableName
    {
        get => _screen.ScreenFoundYVariableName;
        set => SetScriptField(ref _screen.ScreenFoundYVariableName, value?.Trim() ?? string.Empty);
    }

    public string ImageAssetName
    {
        get => _screen.ImageAssetName;
        set => SetScriptField(ref _screen.ImageAssetName, value?.Trim() ?? string.Empty);
    }

    public double ImageSearchSimilarity
    {
        get => _screen.ImageSearchSimilarity;
        set => SetScriptField(ref _screen.ImageSearchSimilarity, value);
    }

    public EditorImageMatchMode ImageSearchMatchMode
    {
        get => _screen.ImageSearchMatchMode;
        set => SetScriptField(ref _screen.ImageSearchMatchMode, value);
    }

    public bool ImageSearchMatchModeWasExplicit { get => _screen.ImageSearchMatchModeWasExplicit; set => _screen.ImageSearchMatchModeWasExplicit = value; }

    public void SetImageSearchMatchMode(EditorImageMatchMode value, bool wasExplicit = true)
    {
        ImageSearchMatchMode = value;
        ImageSearchMatchModeWasExplicit = wasExplicit;
        MarkStructuredScriptEdited();
    }

    public bool TryGetScreenReadingPayload(out EditorActionScreenReadingPayload payload)
    {
        return EditorActionScreenReadingPayload.TryCreate(this, out payload);
    }

    public void ApplyScreenReadingPayload(EditorActionScreenReadingPayload payload)
    {
        if (!EditorActionScreenReadingPayload.IsScreenReadingAction(payload.Type))
        {
            throw new ArgumentException("Payload type must be a screen-reading action.", nameof(payload));
        }

        Type = payload.Type;
        IsAbsolute = payload.IsAbsolute;
        ScreenX = payload.ScreenX;
        ScreenY = payload.ScreenY;
        ScreenLeft = payload.ScreenLeft;
        ScreenTop = payload.ScreenTop;
        ScreenWidth = payload.ScreenWidth;
        ScreenHeight = payload.ScreenHeight;
        ScreenColorHex = payload.ScreenColorHex;
        ScreenTargetColorSource = payload.ScreenTargetColorSource;
        ScreenTargetColorVariableName = payload.ScreenTargetColorVariableName;
        ScreenColorVariableName = payload.ScreenColorVariableName;
        ScreenTimeoutMs = payload.ScreenTimeoutMs;
        ScreenTolerance = payload.ScreenTolerance;
        ScreenFoundVariableName = payload.ScreenFoundVariableName;
        ScreenFoundXVariableName = payload.ScreenFoundXVariableName;
        ScreenFoundYVariableName = payload.ScreenFoundYVariableName;
        ImageAssetName = payload.ImageAssetName;
        ImageSearchSimilarity = payload.ImageSearchSimilarity;
        ImageSearchMatchMode = payload.ImageSearchMatchMode;
        ImageSearchMatchModeWasExplicit = payload.ImageSearchMatchModeWasExplicit;
        Button = payload.Button;
    }

    private static string NormalizeColorHex(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
