namespace CrossMacro.Platform.Linux.Tests.Native.UInput;

public sealed class UInputDeviceLifetimeTests
{
    [Fact]
    public void WritingAfterDispose_ThrowsForEveryEntryPoint()
    {
        var device = new UInputDevice(width: 1920, height: 1080);
        device.Dispose();

        _ = Assert.Throws<ObjectDisposedException>(() => device.SendEvent(UInputNative.EV_KEY, 30, 1));
        _ = Assert.Throws<ObjectDisposedException>(() => device.Move(1, 1));
        _ = Assert.Throws<ObjectDisposedException>(() => device.MoveAbsolute(1, 1));
        _ = Assert.Throws<ObjectDisposedException>(() => device.EmitButton(UInputNative.BTN_LEFT, pressed: true));
        _ = Assert.Throws<ObjectDisposedException>(() => device.EmitKey(30, pressed: true));
    }

    [Fact]
    public void WritingBeforeCreation_ThrowsInvalidOperation()
    {
        using var device = new UInputDevice();

        _ = Assert.Throws<InvalidOperationException>(() => device.Move(1, 1));
    }
}
