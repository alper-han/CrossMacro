using CrossMacro.Platform.MacOS.Services.Permissions;

namespace CrossMacro.Platform.MacOS.Tests.Services;

internal sealed class RecordingPermissionNative : IMacOSPermissionNative
{
    private int _screenCapturePreflightIndex;

    public bool AccessibilityTrusted { get; init; }

    public bool ListenEventGranted { get; init; }

    public bool PostEventGranted { get; init; }

    public bool[] ScreenCapturePreflightResults { get; init; } = [false];

    public bool IOHIDRequestResult { get; init; }

    public bool CoreGraphicsListenRequestResult { get; init; }

    /// <summary>Every native call in order, e.g. <c>RequestPostEvent</c> or <c>OpenPrivacySettings:Privacy_ListenEvent</c>.</summary>
    public List<string> Calls { get; } = [];

    public int Count(string call) => Calls.Count(entry => string.Equals(entry, call, StringComparison.Ordinal));

    public bool IsAccessibilityTrusted() => Record(nameof(IsAccessibilityTrusted), AccessibilityTrusted);

    public bool PromptAccessibility() => Record(nameof(PromptAccessibility), AccessibilityTrusted);

    public bool PreflightListenEvent() => Record(nameof(PreflightListenEvent), ListenEventGranted);

    public bool RequestListenEventWithIOHID() => Record(nameof(RequestListenEventWithIOHID), IOHIDRequestResult);

    public bool RequestListenEventWithCoreGraphics() => Record(nameof(RequestListenEventWithCoreGraphics), CoreGraphicsListenRequestResult);

    public bool PreflightPostEvent() => Record(nameof(PreflightPostEvent), PostEventGranted);

    public bool RequestPostEvent() => Record(nameof(RequestPostEvent), PostEventGranted);

    public bool PreflightScreenCapture()
    {
        var index = Math.Min(_screenCapturePreflightIndex++, ScreenCapturePreflightResults.Length - 1);
        return Record(nameof(PreflightScreenCapture), ScreenCapturePreflightResults[index]);
    }

    public bool RequestScreenCapture() => Record(nameof(RequestScreenCapture), result: true);

    public void OpenPrivacySettings(string pane) => Calls.Add($"{nameof(OpenPrivacySettings)}:{pane}");

    private bool Record(string call, bool result)
    {
        Calls.Add(call);
        return result;
    }
}
