using CrossMacro.Platform.MacOS.Services;
using CrossMacro.Platform.Abstractions;
using System.Runtime.Versioning;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

[SupportedOSPlatform("macos")]
public class MacOSPermissionCheckerServiceTests
{
    [Fact]
    public void IsSupported_ShouldAlwaysBeTrue()
    {
        var checker = new MacOSPermissionCheckerService();

        Assert.True(checker.IsSupported);
    }

    [Fact]
    public void RequiresStartupPermissionGate_ShouldBeTrue()
    {
        var checker = new MacOSPermissionCheckerService();

        Assert.True(checker.RequiresStartupPermissionGate);
    }

    [Fact]
    public void CheckUInputAccess_ShouldAlwaysReturnFalse()
    {
        var checker = new MacOSPermissionCheckerService();

        Assert.False(checker.CheckUInputAccess());
    }


    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GetCurrentStatus_ReturnsIndependentListenAndPostStates(
        bool listenGranted,
        bool postGranted)
    {
        var expectedStatus = new MacOSPermissionStatus(
            ListenEventGranted: listenGranted,
            PostEventGranted: postGranted,
            AccessibilityGranted: false);
        var checker = new MacOSPermissionCheckerService(
            getCurrentStatus: () => expectedStatus,
            isAccessibilityTrusted: () => false);

        var status = checker.GetCurrentStatus();

        Assert.Equal(listenGranted, status.ListenEventGranted);
        Assert.Equal(postGranted, status.PostEventGranted);
        Assert.False(status.AccessibilityGranted);
        Assert.Equal(listenGranted, checker.IsListenEventAccessGranted());
        Assert.Equal(postGranted, checker.IsPostEventAccessGranted());
    }

    [Fact]
    public void IsPermissionGranted_WhenAccessApisUnavailable_TreatsListenAndPostAsNotGranted()
    {
        var checker = new MacOSPermissionCheckerService(
            getCurrentStatus: () => new MacOSPermissionStatus(
                ListenEventGranted: true,
                PostEventGranted: true,
                AccessibilityGranted: true,
                ListenEventApiAvailable: false,
                PostEventApiAvailable: false),
            isAccessibilityTrusted: () => true);

        Assert.False(checker.IsPermissionGranted(MacOSPermissionRequirement.ListenEvent));
        Assert.False(checker.IsPermissionGranted(MacOSPermissionRequirement.PostEvent));
        Assert.True(checker.IsPermissionGranted(MacOSPermissionRequirement.Accessibility));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsAccessibilityTrusted_PreservesCompatibilityPath(bool accessibilityTrusted)
    {
        var checker = new MacOSPermissionCheckerService(
            getCurrentStatus: () => new MacOSPermissionStatus(false, false, !accessibilityTrusted),
            isAccessibilityTrusted: () => accessibilityTrusted);

        Assert.Equal(accessibilityTrusted, checker.IsAccessibilityTrusted());
    }

    [Fact]
    public void RequestPermission_WhenListenEventRequested_CallsOnlyListenRequestFlow()
    {
        var listenRequests = 0;
        var postRequests = 0;
        var checker = new MacOSPermissionCheckerService(
            getCurrentStatus: () => new MacOSPermissionStatus(false, false, false),
            isAccessibilityTrusted: () => false,
            requestListenEventAccess: () =>
            {
                listenRequests++;
                return true;
            },
            requestPostEventAccess: () =>
            {
                postRequests++;
                return true;
            });

        Assert.True(checker.RequestPermission(MacOSPermissionRequirement.ListenEvent));
        Assert.True(checker.RequestListenEventAccess());
        Assert.Equal(2, listenRequests);
        Assert.Equal(0, postRequests);
    }

    [Fact]
    public void IsPermissionGranted_ForListenEvent_UsesDedicatedListenProbeWithoutAccessibilityProbe()
    {
        var listenProbes = 0;
        var accessibilityProbes = 0;
        var checker = new MacOSPermissionCheckerService(
            getCurrentStatus: () => throw new InvalidOperationException("full status should not be used"),
            isAccessibilityTrusted: () =>
            {
                accessibilityProbes++;
                return true;
            },
            isListenEventAccessGranted: () =>
            {
                listenProbes++;
                return false;
            });

        Assert.False(checker.IsPermissionGranted(MacOSPermissionRequirement.ListenEvent));
        Assert.False(checker.IsListenEventAccessGranted());
        Assert.Equal(2, listenProbes);
        Assert.Equal(0, accessibilityProbes);
    }

    [Fact]
    public void IsPermissionGranted_ForPostEvent_UsesDedicatedPostProbeWithoutAccessibilityProbe()
    {
        var postProbes = 0;
        var accessibilityProbes = 0;
        var checker = new MacOSPermissionCheckerService(
            getCurrentStatus: () => throw new InvalidOperationException("full status should not be used"),
            isAccessibilityTrusted: () =>
            {
                accessibilityProbes++;
                return false;
            },
            isPostEventAccessGranted: () =>
            {
                postProbes++;
                return true;
            });

        Assert.True(checker.IsPermissionGranted(MacOSPermissionRequirement.PostEvent));
        Assert.True(checker.IsPostEventAccessGranted());
        Assert.Equal(2, postProbes);
        Assert.Equal(0, accessibilityProbes);
    }

    [Fact]
    public void RequestPermission_WhenPostEventRequested_CallsOnlyPostRequestFlow()
    {
        var listenRequests = 0;
        var postRequests = 0;
        var checker = new MacOSPermissionCheckerService(
            getCurrentStatus: () => new MacOSPermissionStatus(false, false, false),
            isAccessibilityTrusted: () => false,
            requestListenEventAccess: () =>
            {
                listenRequests++;
                return true;
            },
            requestPostEventAccess: () =>
            {
                postRequests++;
                return true;
            });

        Assert.True(checker.RequestPermission(MacOSPermissionRequirement.PostEvent));
        Assert.True(checker.RequestPostEventAccess());
        Assert.Equal(0, listenRequests);
        Assert.Equal(2, postRequests);
    }

    [Theory]
    [InlineData(MacOSPermissionRequirement.ListenEvent, true, false, false)]
    [InlineData(MacOSPermissionRequirement.PostEvent, false, true, false)]
    [InlineData(MacOSPermissionRequirement.Accessibility, false, false, true)]
    public void IsGranted_WhenOnlyExpectedPermissionIsGranted_ReturnsTrueOnlyForThatPermission(
        MacOSPermissionRequirement grantedRequirement,
        bool listenGranted,
        bool postGranted,
        bool accessibilityGranted)
    {
        var status = new MacOSPermissionStatus(listenGranted, postGranted, accessibilityGranted);

        foreach (var requirement in Enum.GetValues<MacOSPermissionRequirement>())
        {
            Assert.Equal(grantedRequirement == requirement, status.IsGranted(requirement));
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void IsGranted_WhenCheckingListenEvent_RequiresGrantedPermissionAndAvailableApi(
        bool listenGranted,
        bool apiAvailable,
        bool expectedGranted)
    {
        var status = new MacOSPermissionStatus(
            ListenEventGranted: listenGranted,
            PostEventGranted: true,
            AccessibilityGranted: true,
            ListenEventApiAvailable: apiAvailable);

        Assert.Equal(expectedGranted, status.IsGranted(MacOSPermissionRequirement.ListenEvent));
        Assert.True(status.IsGranted(MacOSPermissionRequirement.PostEvent));
        Assert.True(status.IsGranted(MacOSPermissionRequirement.Accessibility));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void IsGranted_WhenCheckingPostEvent_RequiresGrantedPermissionAndAvailableApi(
        bool postGranted,
        bool apiAvailable,
        bool expectedGranted)
    {
        var status = new MacOSPermissionStatus(
            ListenEventGranted: true,
            PostEventGranted: postGranted,
            AccessibilityGranted: true,
            PostEventApiAvailable: apiAvailable);

        Assert.True(status.IsGranted(MacOSPermissionRequirement.ListenEvent));
        Assert.Equal(expectedGranted, status.IsGranted(MacOSPermissionRequirement.PostEvent));
        Assert.True(status.IsGranted(MacOSPermissionRequirement.Accessibility));
    }

    [Fact]
    public void ForFlow_WhenCaptureOnly_RequiresListenButNotPostOrAccessibility()
    {
        var plan = MacOSPermissionPlan.ForFlow(
            capturesInput: true,
            playsBackInput: false,
            usesAccessibilityFeatures: false);

        Assert.True(plan.RequiresListenEvent);
        Assert.False(plan.RequiresPostEvent);
        Assert.False(plan.RequiresAccessibility);
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(true, false, false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(false, true, true)));
    }

    [Fact]
    public void ForFlow_WhenPlaybackOnly_RequiresPostButNotListenOrAccessibility()
    {
        var plan = MacOSPermissionPlan.ForFlow(
            capturesInput: false,
            playsBackInput: true,
            usesAccessibilityFeatures: false);

        Assert.False(plan.RequiresListenEvent);
        Assert.True(plan.RequiresPostEvent);
        Assert.False(plan.RequiresAccessibility);
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(false, true, false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(true, false, true)));
    }

    [Fact]
    public void ForFlow_WhenPlaybackAndCapture_RequiresPostAndListen()
    {
        var plan = MacOSPermissionPlan.ForFlow(
            capturesInput: true,
            playsBackInput: true,
            usesAccessibilityFeatures: false);

        Assert.True(plan.RequiresListenEvent);
        Assert.True(plan.RequiresPostEvent);
        Assert.False(plan.RequiresAccessibility);
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(true, true, false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(true, false, false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(false, true, false)));
    }

    [Fact]
    public void ForFlow_WhenAccessibilityFeatureUsed_KeepsAccessibilitySeparateFromListenAndPost()
    {
        var plan = MacOSPermissionPlan.ForFlow(
            capturesInput: false,
            playsBackInput: false,
            usesAccessibilityFeatures: true);

        Assert.False(plan.RequiresListenEvent);
        Assert.False(plan.RequiresPostEvent);
        Assert.True(plan.RequiresAccessibility);
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(false, false, true)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(true, true, false)));
    }
}
