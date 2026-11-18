namespace CrossMacro.Editing.Models;

public partial class EditorAction
{
    public string ScreenshotOutputPath
    {
        get => _screenshot.ScreenshotOutputPath;
        set => SetScriptField(ref _screenshot.ScreenshotOutputPath, value ?? string.Empty);
    }

    public bool ScreenshotCopyToClipboard
    {
        get => _screenshot.ScreenshotCopyToClipboard;
        set => SetScriptField(ref _screenshot.ScreenshotCopyToClipboard, value);
    }

    public bool ScreenshotUseRegion
    {
        get => _screenshot.ScreenshotUseRegion;
        set => SetScriptField(ref _screenshot.ScreenshotUseRegion, value);
    }

    public string ScreenshotRegionX
    {
        get => _screenshot.ScreenshotRegionX;
        set => SetScriptField(ref _screenshot.ScreenshotRegionX, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionY
    {
        get => _screenshot.ScreenshotRegionY;
        set => SetScriptField(ref _screenshot.ScreenshotRegionY, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionWidth
    {
        get => _screenshot.ScreenshotRegionWidth;
        set => SetScriptField(ref _screenshot.ScreenshotRegionWidth, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionHeight
    {
        get => _screenshot.ScreenshotRegionHeight;
        set => SetScriptField(ref _screenshot.ScreenshotRegionHeight, value?.Trim() ?? string.Empty);
    }

    public bool TryGetScreenshotStep(out ScreenshotStep step)
    {
        step = new ScreenshotStep(
            ScreenshotOutputPath,
            ScreenshotCopyToClipboard,
            ScreenshotUseRegion,
            ScreenshotRegionX,
            ScreenshotRegionY,
            ScreenshotRegionWidth,
            ScreenshotRegionHeight);
        return Type is EditorActionType.Screenshot;
    }
}
