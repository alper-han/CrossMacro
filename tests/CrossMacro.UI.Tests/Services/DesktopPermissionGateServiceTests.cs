using Avalonia.Controls.ApplicationLifetimes;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI;
using CrossMacro.UI.Services;
using NSubstitute;

namespace CrossMacro.UI.Tests.Services;

public sealed class DesktopPermissionGateServiceTests
{
    [Fact]
    public void IsStartupPermissionBlocked_WhenCheckerUnsupported_ReturnsFalse()
    {
        var checker = Substitute.For<IPermissionChecker>();
        checker.IsSupported.Returns(false);

        Assert.False(DesktopPermissionGateService.IsStartupPermissionBlocked(checker));
    }

    [Fact]
    public void IsStartupPermissionBlocked_WhenStartupGateNotRequired_ReturnsFalse()
    {
        var checker = Substitute.For<IPermissionChecker>();
        checker.IsSupported.Returns(true);
        checker.RequiresStartupPermissionGate.Returns(false);

        Assert.False(DesktopPermissionGateService.IsStartupPermissionBlocked(checker));
    }

    [Fact]
    public void IsStartupPermissionBlocked_WhenAccessibilityUntrusted_ReturnsTrue()
    {
        var checker = Substitute.For<IPermissionChecker>();
        checker.IsSupported.Returns(true);
        checker.RequiresStartupPermissionGate.Returns(true);
        checker.IsAccessibilityTrusted().Returns(false);

        Assert.True(DesktopPermissionGateService.IsStartupPermissionBlocked(checker));
    }

    [Fact]
    public void GetStartupPermissionGateKind_WhenMacOSStatusReportsListenMissing_BlocksForInputMonitoring()
    {
        var checker = new TestMacOSPermissionChecker(
            listenEventGranted: false,
            postEventGranted: true,
            accessibilityGranted: true);

        var gateKind = DesktopPermissionGateService.GetStartupPermissionGateKind(checker);

        Assert.Equal(DesktopPermissionGateService.StartupPermissionGateKind.InputMonitoring, gateKind);
    }

    [Fact]
    public void GetStartupPermissionGateKind_WhenMacOSStatusReportsListenGranted_BlocksForMissingPlaybackPermission()
    {
        var checker = new TestMacOSPermissionChecker(
            listenEventGranted: true,
            postEventGranted: false,
            accessibilityGranted: false);

        var gateKind = DesktopPermissionGateService.GetStartupPermissionGateKind(checker);

        Assert.Equal(DesktopPermissionGateService.StartupPermissionGateKind.Accessibility, gateKind);
    }

    [Fact]
    public void GetStartupPermissionGateKind_WhenMacOSStatusReportsListenAndPlaybackGranted_DoesNotBlock()
    {
        var checker = new TestMacOSPermissionChecker(
            listenEventGranted: true,
            postEventGranted: false,
            accessibilityGranted: true);

        var gateKind = DesktopPermissionGateService.GetStartupPermissionGateKind(checker);

        Assert.Equal(DesktopPermissionGateService.StartupPermissionGateKind.None, gateKind);
    }

    [Fact]
    public void GetStartupPermissionGateKind_WhenMacOSStatusReportsListenApiUnavailable_BlocksForInputMonitoring()
    {
        var checker = new TestMacOSPermissionChecker(
            listenEventGranted: true,
            postEventGranted: true,
            accessibilityGranted: true,
            listenEventApiAvailable: false);

        var gateKind = DesktopPermissionGateService.GetStartupPermissionGateKind(checker);

        Assert.Equal(DesktopPermissionGateService.StartupPermissionGateKind.InputMonitoring, gateKind);
    }

    [Fact]
    public void GetStartupPermissionGateKind_WhenMacOSStatusProbeThrows_FallsBackToAccessibilityGate()
    {
        var checker = new ThrowingStatusPermissionChecker(
            listenEventGranted: true,
            postEventGranted: false,
            accessibilityGranted: false);

        var gateKind = DesktopPermissionGateService.GetStartupPermissionGateKind(checker);

        Assert.Equal(DesktopPermissionGateService.StartupPermissionGateKind.Accessibility, gateKind);
    }

    [Fact]
    public void GetStartupPermissionGateKind_WhenMacOSListenEventMissing_DoesNotProbeAccessibilityFirst()
    {
        var checker = new ThrowingStatusPermissionChecker(
            listenEventGranted: false,
            postEventGranted: true,
            accessibilityGranted: true);

        var gateKind = DesktopPermissionGateService.GetStartupPermissionGateKind(checker);

        Assert.Equal(DesktopPermissionGateService.StartupPermissionGateKind.InputMonitoring, gateKind);
        Assert.Equal(0, checker.AccessibilityProbeCount);
        Assert.Equal(0, checker.PostEventProbeCount);
    }

    [Fact]
    public void MacOSStartupPermissionMessage_ExplainsTwoStepPermissionSetup()
    {
        var message = UIStrings.MacOSInputMonitoringStartupBlockMessage;

        Assert.Contains("Input Monitoring", message);
        Assert.Contains("read", message);
        Assert.Contains("recording", message);
        Assert.Contains("Accessibility", message);
        Assert.Contains("play macros back", message);
        Assert.Contains("correct System Settings page", message);
        Assert.Contains("do not need to add the app by hand", message);
    }

    [Fact]
    public void MacOSAccessibilityStartupBlockMessage_ExplainsPlaybackPermissionStep()
    {
        var message = UIStrings.MacOSAccessibilityStartupBlockMessage;

        Assert.Contains("playback", message);
        Assert.Contains("Accessibility", message);
        Assert.Contains("Input Monitoring", message);
        Assert.Contains("recording", message);
        Assert.Contains("current app", message);
        Assert.Contains("check again", message);
    }

    [Fact]
    public void MacOSPermissionApprovalRecheckMessage_ExplainsContinueFlow()
    {
        var message = UIStrings.MacOSPermissionApprovalRecheckMessage;

        Assert.Contains("Approve CrossMacro", message);
        Assert.Contains("click Continue", message);
        Assert.Contains("continue automatically", message);
    }

    [Fact]
    public void MacOSApprovalPendingMessages_ExplainRestartFallback()
    {
        Assert.Contains("Input Monitoring", UIStrings.MacOSInputMonitoringApprovalPendingMessage);
        Assert.Contains("quit and reopen CrossMacro", UIStrings.MacOSInputMonitoringApprovalPendingMessage);
        Assert.Contains("Accessibility", UIStrings.MacOSAccessibilityApprovalPendingMessage);
        Assert.Contains("quit and reopen CrossMacro", UIStrings.MacOSAccessibilityApprovalPendingMessage);
    }

    [Fact]
    public void OpenStartupPermissionSettings_WhenInputMonitoringGate_RequestsAndOpensInputMonitoringSettings()
    {
        var checker = new TestMacOSPermissionChecker(
            listenEventGranted: false,
            postEventGranted: true,
            accessibilityGranted: true);

        DesktopPermissionGateService.OpenStartupPermissionSettings(
            checker,
            DesktopPermissionGateService.StartupPermissionGateKind.InputMonitoring);

        Assert.Equal(1, checker.ListenEventRequestCount);
        Assert.Equal(1, checker.InputMonitoringSettingsOpenCount);
        Assert.Equal(0, checker.AccessibilitySettingsOpenCount);
    }

    [Fact]
    public void OpenStartupPermissionSettings_WhenAccessibilityGate_RequestsAndOpensAccessibilitySettings()
    {
        var checker = new TestMacOSPermissionChecker(
            listenEventGranted: true,
            postEventGranted: false,
            accessibilityGranted: false);

        DesktopPermissionGateService.OpenStartupPermissionSettings(
            checker,
            DesktopPermissionGateService.StartupPermissionGateKind.Accessibility);

        Assert.Equal(0, checker.ListenEventRequestCount);
        Assert.Equal(1, checker.AccessibilityRequestCount);
        Assert.Equal(0, checker.InputMonitoringSettingsOpenCount);
        Assert.Equal(1, checker.AccessibilitySettingsOpenCount);
    }

    [Fact]
    public async Task TryHandleAsync_WhenSessionUnsupported_ReturnsUnsupportedReasonWithoutHandling()
    {
        var displaySessionService = Substitute.For<IDisplaySessionService>();
        displaySessionService.IsSessionSupported(out Arg.Any<string>())
            .Returns(ci =>
            {
                ci[0] = "unsupported session";
                return false;
            });

        var service = new DesktopPermissionGateService(displaySessionService, () => null);
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();

        var result = await service.TryHandleAsync(desktop);

        Assert.False(result.Handled);
        Assert.Equal("unsupported session", result.UnsupportedSessionReason);
    }

    private sealed class TestMacOSPermissionChecker : IMacOSPermissionChecker
    {
        private readonly bool _listenEventGranted;
        private readonly bool _postEventGranted;
        private readonly bool _accessibilityGranted;
        private readonly bool _listenEventApiAvailable;
        private readonly bool _postEventApiAvailable;

        internal TestMacOSPermissionChecker(
            bool listenEventGranted,
            bool postEventGranted,
            bool accessibilityGranted,
            bool listenEventApiAvailable = true,
            bool postEventApiAvailable = true)
        {
            _listenEventGranted = listenEventGranted;
            _postEventGranted = postEventGranted;
            _accessibilityGranted = accessibilityGranted;
            _listenEventApiAvailable = listenEventApiAvailable;
            _postEventApiAvailable = postEventApiAvailable;
        }

        public bool IsSupported => true;
        public bool RequiresStartupPermissionGate => true;
        public int ListenEventRequestCount { get; private set; }
        public int AccessibilityRequestCount { get; private set; }
        public int InputMonitoringSettingsOpenCount { get; private set; }
        public int AccessibilitySettingsOpenCount { get; private set; }

        public MacOSPermissionStatus GetCurrentStatus()
        {
            return new MacOSPermissionStatus(
                ListenEventGranted: _listenEventGranted,
                PostEventGranted: _postEventGranted,
                AccessibilityGranted: _accessibilityGranted,
                ListenEventApiAvailable: _listenEventApiAvailable,
                PostEventApiAvailable: _postEventApiAvailable);
        }

        public bool IsAccessibilityTrusted()
        {
            return _accessibilityGranted;
        }

        public bool IsListenEventAccessGranted()
        {
            return _listenEventApiAvailable && _listenEventGranted;
        }

        public bool IsPostEventAccessGranted()
        {
            return _postEventApiAvailable && _postEventGranted;
        }

        public bool CheckUInputAccess()
        {
            return false;
        }

        public void OpenAccessibilitySettings()
        {
            AccessibilitySettingsOpenCount++;
        }

        public bool RequestListenEventAccess()
        {
            ListenEventRequestCount++;
            return true;
        }

        public bool RequestPostEventAccess()
        {
            return true;
        }

        public bool RequestPermission(MacOSPermissionRequirement requirement)
        {
            return requirement switch
            {
                MacOSPermissionRequirement.ListenEvent => RequestListenEventAccess(),
                MacOSPermissionRequirement.PostEvent => RequestPostEventAccess(),
                MacOSPermissionRequirement.Accessibility => RequestAccessibilityPermission(),
                _ => false
            };
        }

        public bool IsPermissionGranted(MacOSPermissionRequirement requirement)
        {
            return requirement switch
            {
                MacOSPermissionRequirement.ListenEvent => IsListenEventAccessGranted(),
                MacOSPermissionRequirement.PostEvent => IsPostEventAccessGranted(),
                MacOSPermissionRequirement.Accessibility => IsAccessibilityTrusted(),
                _ => false
            };
        }

        public void OpenInputMonitoringSettings()
        {
            InputMonitoringSettingsOpenCount++;
        }

        private bool RequestAccessibilityPermission()
        {
            AccessibilityRequestCount++;
            return true;
        }
    }

    private sealed class ThrowingStatusPermissionChecker : IMacOSPermissionChecker
    {
        private readonly bool _listenEventGranted;
        private readonly bool _postEventGranted;
        private readonly bool _accessibilityGranted;

        internal ThrowingStatusPermissionChecker(bool listenEventGranted, bool postEventGranted, bool accessibilityGranted)
        {
            _listenEventGranted = listenEventGranted;
            _postEventGranted = postEventGranted;
            _accessibilityGranted = accessibilityGranted;
        }

        public int AccessibilityProbeCount { get; private set; }
        public int PostEventProbeCount { get; private set; }

        public bool IsSupported => true;
        public bool RequiresStartupPermissionGate => true;

        public MacOSPermissionStatus GetCurrentStatus()
        {
            throw new InvalidOperationException("status unavailable");
        }

        public bool IsPermissionGranted(MacOSPermissionRequirement requirement)
        {
            return false;
        }

        public bool RequestPermission(MacOSPermissionRequirement requirement)
        {
            return false;
        }

        public bool RequestListenEventAccess()
        {
            return false;
        }

        public bool RequestPostEventAccess()
        {
            return false;
        }

        public bool IsAccessibilityTrusted()
        {
            AccessibilityProbeCount++;
            return _accessibilityGranted;
        }

        public bool IsListenEventAccessGranted()
        {
            return _listenEventGranted;
        }

        public bool IsPostEventAccessGranted()
        {
            PostEventProbeCount++;
            return _postEventGranted;
        }

        public bool CheckUInputAccess()
        {
            return false;
        }

        public void OpenAccessibilitySettings()
        {
        }

        public void OpenInputMonitoringSettings()
        {
        }
    }
}
