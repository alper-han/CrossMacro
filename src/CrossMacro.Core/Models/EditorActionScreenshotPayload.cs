
namespace CrossMacro.Core.Models;

public readonly record struct EditorActionScreenshotPayload(
    string OutputPath,
    bool CopyToClipboard,
    bool UseRegion,
    string RegionX,
    string RegionY,
    string RegionWidth,
    string RegionHeight)
{
    public static bool TryCreate(EditorAction action, out EditorActionScreenshotPayload payload)
    {
        ArgumentNullException.ThrowIfNull(action);
        payload = new EditorActionScreenshotPayload(
            action.ScreenshotOutputPath,
            action.ScreenshotCopyToClipboard,
            action.ScreenshotUseRegion,
            action.ScreenshotRegionX,
            action.ScreenshotRegionY,
            action.ScreenshotRegionWidth,
            action.ScreenshotRegionHeight);
        return action.Type is EditorActionType.Screenshot;
    }
}
