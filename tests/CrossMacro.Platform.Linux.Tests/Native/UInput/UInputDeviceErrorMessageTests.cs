
namespace CrossMacro.Platform.Linux.Tests.Native.UInput;

public sealed class UInputDeviceErrorMessageTests
{
    [Fact]
    public void ThrowIfEventWriteIncomplete_WhenWriteIsPartial_ThrowsWithTransportDetails()
    {
        var exception = Assert.Throws<IOException>(() => UInputDevice.ThrowIfEventWriteIncomplete(
            type: UInputNative.EV_ABS,
            code: UInputNative.ABS_X,
            value: 42,
            expectedBytes: 24,
            actualBytes: 12,
            errno: 5));

        Assert.Contains("ExpectedBytes=24", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ActualBytes=12", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Errno=5", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowIfEventWriteIncomplete_WhenWriteCompletes_DoesNotThrow()
    {
        var exception = Record.Exception(() => UInputDevice.ThrowIfEventWriteIncomplete(
            type: UInputNative.EV_SYN,
            code: UInputNative.SYN_REPORT,
            value: 0,
            expectedBytes: 24,
            actualBytes: 24,
            errno: 0));

        Assert.Null(exception);
    }

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
