namespace CrossMacro.Platform.Linux.Tests.Native.Evdev;

public sealed class EvdevDeviceCapabilitiesTests
{
    [Fact]
    public void Classification_QueriesEachBitmapOnceAcrossAllPolicies()
    {
        var source = new BitmapSource();
        source.Set(UInputNative.EV_SYN, UInputNative.EV_REL, UInputNative.EV_KEY, UInputNative.EV_ABS);
        source.Set(UInputNative.EV_REL, UInputNative.REL_X, UInputNative.REL_Y);
        source.Set(UInputNative.EV_KEY, UInputNative.BTN_LEFT, InputEventCode.KEY_ENTER, InputEventCode.KEY_A);
        source.Set(UInputNative.EV_ABS, UInputNative.ABS_X, UInputNative.ABS_Y);
        var capabilities = new EvdevDeviceCapabilities(source.Read);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.True(capabilities.IsMouse());
            Assert.True(capabilities.IsKeyboard());
            Assert.False(capabilities.IsTouchpad());
        }

        Assert.Equal(4, source.Calls.Count);
        Assert.All(source.Calls.Values, count => Assert.Equal(1, count));
    }

    [Theory]
    [InlineData(InputEventCode.KEY_A, true)]
    [InlineData(InputEventCode.KEY_Z, true)]
    [InlineData(InputEventCode.KEY_A - 1, false)]
    [InlineData(InputEventCode.KEY_Z + 1, false)]
    public void KeyboardPolicy_PreservesTheExistingLetterRange(int key, bool expected)
    {
        var source = new BitmapSource();
        source.Set(UInputNative.EV_SYN, UInputNative.EV_KEY);
        source.Set(UInputNative.EV_KEY, InputEventCode.KEY_ESC, key);

        Assert.Equal(expected, new EvdevDeviceCapabilities(source.Read).IsKeyboard());
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void TouchpadPolicy_AcceptsAbsoluteOrMultitouchPositionAndRejectsRelativeDevices(bool multiTouch, bool relative, bool expected)
    {
        var source = new BitmapSource();
        source.Set(UInputNative.EV_SYN, relative
            ? [UInputNative.EV_ABS, UInputNative.EV_KEY, UInputNative.EV_REL]
            : [UInputNative.EV_ABS, UInputNative.EV_KEY]);
        source.Set(UInputNative.EV_KEY, UInputNative.BTN_TOUCH);
        source.Set(UInputNative.EV_ABS, multiTouch
            ? [UInputNative.ABS_MT_POSITION_X, UInputNative.ABS_MT_POSITION_Y]
            : [UInputNative.ABS_X, UInputNative.ABS_Y]);

        Assert.Equal(expected, new EvdevDeviceCapabilities(source.Read).IsTouchpad());
    }

    [Fact]
    public void Snapshot_IsStableWithinAnInspectionAndRefreshesForANewInspection()
    {
        var source = new BitmapSource();
        var first = new EvdevDeviceCapabilities(source.Read);
        Assert.False(first.HasCapability(UInputNative.EV_KEY, InputEventCode.KEY_A));
        source.Set(UInputNative.EV_KEY, InputEventCode.KEY_A);

        Assert.False(first.HasCapability(UInputNative.EV_KEY, InputEventCode.KEY_A));
        Assert.True(new EvdevDeviceCapabilities(source.Read).HasCapability(UInputNative.EV_KEY, InputEventCode.KEY_A));
        Assert.Equal(2, source.Calls[UInputNative.EV_KEY]);
    }

    [Fact]
    public void FailedBitmap_IsUnavailableWithoutRepeatedIoAndInvalidCodesDoNotQueryNative()
    {
        var calls = 0;
        var capabilities = new EvdevDeviceCapabilities((_, _) =>
        {
            calls++;
            return -1;
        });

        Assert.False(capabilities.HasCapability(UInputNative.EV_KEY, -1));
        Assert.False(capabilities.HasCapability(UInputNative.EV_KEY, EvdevDeviceCapabilities.MaximumKeyCode + 1));
        Assert.Equal(0, calls);
        Assert.False(capabilities.HasCapability(UInputNative.EV_KEY, InputEventCode.KEY_A));
        Assert.False(capabilities.HasCapability(UInputNative.EV_KEY, InputEventCode.KEY_Z));
        Assert.Equal(1, calls);
    }

    private sealed class BitmapSource
    {
        private readonly Dictionary<int, int[]> _bits = [];
        public Dictionary<int, int> Calls { get; } = [];

        public void Set(int eventType, params int[] codes) => _bits[eventType] = codes;

        public int Read(int eventType, byte[] target)
        {
            _ = Calls.TryGetValue(eventType, out var calls);
            Calls[eventType] = calls + 1;
            if (_bits.TryGetValue(eventType, out var codes))
            {
                foreach (var code in codes)
                {
                    target[code / 8] |= (byte)(1 << (code % 8));
                }
            }
            return target.Length;
        }
    }
}
