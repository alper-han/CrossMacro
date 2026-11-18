
namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSPermissionCheckerServiceTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void GetCurrentStatus_ReportsEachPermissionIndependently(bool listen, bool post, bool accessibility)
    {
        var native = new RecordingPermissionNative
        {
            ListenEventGranted = listen,
            PostEventGranted = post,
            AccessibilityTrusted = accessibility,
        };
        var checker = new MacOSPermissionCheckerService(native);

        var status = checker.GetCurrentStatus();

        Assert.Equal(listen, status.ListenEventGranted);
        Assert.Equal(post, status.PostEventGranted);
        Assert.Equal(accessibility, status.AccessibilityGranted);
        Assert.False(status.ScreenRecordingGranted);
    }

    [Theory]
    [InlineData(MacOSPermissionRequirement.ListenEvent, "PreflightListenEvent")]
    [InlineData(MacOSPermissionRequirement.PostEvent, "PreflightPostEvent")]
    [InlineData(MacOSPermissionRequirement.Accessibility, "IsAccessibilityTrusted")]
    [InlineData(MacOSPermissionRequirement.ScreenRecording, "PreflightScreenCapture")]
    public void IsPermissionGranted_ProbesOnlyTheRequestedPermission(MacOSPermissionRequirement requirement, string expectedProbe)
    {
        var native = new RecordingPermissionNative();
        var checker = new MacOSPermissionCheckerService(native);

        _ = checker.IsPermissionGranted(requirement);

        Assert.Equal([expectedProbe], native.Calls);
    }

    [Fact]
    public void RequestListenEventAccess_WhenIOHIDGrants_DoesNotAskCoreGraphics()
    {
        var native = new RecordingPermissionNative { IOHIDRequestResult = true };
        var checker = new MacOSPermissionCheckerService(native);

        Assert.True(checker.RequestListenEventAccess());

        Assert.Equal(["RequestListenEventWithIOHID"], native.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RequestListenEventAccess_WhenIOHIDDoesNotGrant_ReturnsCoreGraphicsAnswer(bool coreGraphicsResult)
    {
        var native = new RecordingPermissionNative { CoreGraphicsListenRequestResult = coreGraphicsResult };
        var checker = new MacOSPermissionCheckerService(native);

        Assert.Equal(coreGraphicsResult, checker.RequestListenEventAccess());

        Assert.Equal(["RequestListenEventWithIOHID", "RequestListenEventWithCoreGraphics"], native.Calls);
    }

    [Fact]
    public void RequestPostEventAccess_RaisesOnlyThePostEventPrompt()
    {
        var native = new RecordingPermissionNative();
        var checker = new MacOSPermissionCheckerService(native);

        Assert.False(checker.RequestPostEventAccess());

        Assert.Equal(["RequestPostEvent"], native.Calls);
    }

    [Theory]
    [InlineData(MacOSPermissionRequirement.ListenEvent, "RequestListenEventWithIOHID")]
    [InlineData(MacOSPermissionRequirement.PostEvent, "RequestPostEvent")]
    [InlineData(MacOSPermissionRequirement.Accessibility, "PromptAccessibility")]
    [InlineData(MacOSPermissionRequirement.ScreenRecording, "RequestScreenCapture")]
    public void RequestPermission_StartsTheMatchingPrompt(MacOSPermissionRequirement requirement, string expectedFirstCall)
    {
        var native = new RecordingPermissionNative();
        var checker = new MacOSPermissionCheckerService(native);

        _ = checker.RequestPermission(requirement);

        Assert.Equal(expectedFirstCall, native.Calls[0]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScreenRecordingProbe_ReportsPreflightResult(bool granted)
    {
        var native = new RecordingPermissionNative { ScreenCapturePreflightResults = [granted] };
        IMacOSScreenRecordingPermissionProbe probe = new MacOSPermissionCheckerService(native);

        Assert.True(probe.IsPreflightAvailable);
        Assert.Equal(granted, probe.IsGranted());
    }

    [Fact]
    public void OpenSettings_OpensTheMatchingPrivacyPane()
    {
        var native = new RecordingPermissionNative();
        var checker = new MacOSPermissionCheckerService(native);

        checker.OpenAccessibilitySettings();
        checker.OpenInputMonitoringSettings();
        checker.OpenScreenRecordingSettings();

        Assert.Equal(
            [
                "OpenPrivacySettings:Privacy_Accessibility",
                "OpenPrivacySettings:Privacy_ListenEvent",
                "OpenPrivacySettings:Privacy_ScreenCapture",
            ],
            native.Calls);
    }
}
