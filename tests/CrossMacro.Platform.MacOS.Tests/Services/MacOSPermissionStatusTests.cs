using CrossMacro.Platform.Abstractions.Permissions;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSPermissionStatusTests
{
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
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: false, AccessibilityGranted: false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: false, PostEventGranted: true, AccessibilityGranted: true)));
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
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: false, PostEventGranted: true, AccessibilityGranted: false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: false, AccessibilityGranted: true)));
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
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: true, AccessibilityGranted: false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: false, AccessibilityGranted: false)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: false, PostEventGranted: true, AccessibilityGranted: false)));
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
        Assert.True(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: false, PostEventGranted: false, AccessibilityGranted: true)));
        Assert.False(plan.IsSatisfiedBy(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: true, AccessibilityGranted: false)));
    }
}
