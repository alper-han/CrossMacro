using CrossMacro.Platform.Linux.Native.UInput;

namespace CrossMacro.Platform.Linux.Tests.Native.UInput;

public class UInputDeviceErrorMessageTests
{
    [Fact]
    public void BuildOpenUInputErrorMessage_WhenErrnoIsNoEntry_ShouldMentionMissingDeviceNode()
    {
        var message = UInputDevice.BuildOpenUInputErrorMessage(2);

        Assert.Contains("device node is missing", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("modprobe uinput", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildOpenUInputErrorMessage_WhenErrnoIsPermissionDenied_ShouldMentionInputGroupAndUdev()
    {
        var message = UInputDevice.BuildOpenUInputErrorMessage(13);

        Assert.Contains("Permission denied", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("input or uinput group", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildOpenUInputErrorMessage_WhenErrnoIsUnexpected_ShouldUseGenericGuidance()
    {
        var message = UInputDevice.BuildOpenUInputErrorMessage(99);

        Assert.Contains("Check that uinput exists", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectOpenUInputErrno_WhenPrimaryIsPermissionDenied_ShouldPreferPrimary()
    {
        var errno = UInputDevice.SelectOpenUInputErrno(primaryErrno: 13, alternateErrno: 2);

        Assert.Equal(13, errno);
    }

    [Fact]
    public void SelectOpenUInputErrno_WhenAlternateIsPermissionDenied_ShouldPreferAlternate()
    {
        var errno = UInputDevice.SelectOpenUInputErrno(primaryErrno: 2, alternateErrno: 13);

        Assert.Equal(13, errno);
    }

    [Fact]
    public void SelectOpenUInputErrno_WhenNoPermissionErrors_ShouldPreferPrimary()
    {
        var errno = UInputDevice.SelectOpenUInputErrno(primaryErrno: 2, alternateErrno: 5);

        Assert.Equal(2, errno);
    }
}
