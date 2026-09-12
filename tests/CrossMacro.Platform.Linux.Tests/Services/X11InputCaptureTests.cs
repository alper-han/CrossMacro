namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class X11InputCaptureTests
{
    [Fact]
    public void XInput2MaskHelpers_HandleInvalidBitsWithoutInvalidMemoryAccess()
    {
        var mask = new byte[1];

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => XInput2Consts.SetMask(mask, -1));
        _ = Assert.Throws<IndexOutOfRangeException>(() => XInput2Consts.SetMask(mask, 8));
        Assert.False(XInput2Consts.IsBitSet(mask, -1));
        _ = Assert.Throws<IndexOutOfRangeException>(() => XInput2Consts.IsBitSet(mask, 8));

        Span<byte> span = stackalloc byte[1];
        Assert.False(XInput2Consts.IsBitSet(span, -1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(SetInvalidSpanBit);
        Assert.False(XInput2Consts.IsBitSet(IntPtr.Zero, maskLen: 0, bit: 0));
        Assert.False(XInput2Consts.IsBitSet(IntPtr.Zero, maskLen: -1, bit: 0));
        Assert.False(XInput2Consts.IsBitSet(IntPtr.Zero, maskLen: 1, bit: -1));
    }

    private static void SetInvalidSpanBit()
    {
        Span<byte> span = stackalloc byte[1];
        XInput2Consts.SetMask(span, -1);
    }

    [Fact]
    public void CreateEventMask_WhenMouseCaptureIsEnabled_SelectsGlobalRawMotion()
    {
        var mask = X11CaptureBase.CreateEventMask(captureMouse: true, captureKeyboard: false);

        Assert.True(XInput2Consts.IsBitSet(mask, XInput2Consts.XI_RawMotion));
        Assert.False(XInput2Consts.IsBitSet(mask, XInput2Consts.XI_Motion));
        Assert.True(XInput2Consts.IsBitSet(mask, XInput2Consts.XI_RawButtonPress));
        Assert.True(XInput2Consts.IsBitSet(mask, XInput2Consts.XI_RawButtonRelease));
    }

    [Fact]
    public void AbsoluteCapture_WhenRawMotionArrives_EmitsQueriedRootCoordinatesAtomically()
    {
        var capture = new TestX11AbsoluteCapture((100, 200), (115, 225));
        var events = new List<CapturedInputEvent>();
        capture.InputReceived += (_, args) => events.Add(args.Event);

        capture.InitializePosition();
        capture.ProcessRawMotion();

        Assert.Collection(
            events,
            x =>
            {
                Assert.Equal(InputEventType.MouseMove, x.Type);
                Assert.Equal(InputEventCode.ABS_X, x.Code);
                Assert.Equal(115, x.Value);
            },
            y =>
            {
                Assert.Equal(InputEventType.MouseMove, y.Type);
                Assert.Equal(InputEventCode.ABS_Y, y.Code);
                Assert.Equal(225, y.Value);
            },
            sync => Assert.Equal(InputEventType.Sync, sync.Type));
        Assert.Equal(events[0].Timestamp, events[1].Timestamp);
        Assert.Equal(events[0].Timestamp, events[2].Timestamp);
    }

    [Fact]
    public void RelativeCapture_WhenRawMotionArrives_EmitsAxesAndSyncWithOneTimestamp()
    {
        using var rawValues = new UnmanagedDoubles(1.5, -2.5);
        using var mask = new UnmanagedBytes(0b11);
        var capture = new TestX11RelativeCapture();
        var events = new List<CapturedInputEvent>();
        capture.InputReceived += (_, args) => events.Add(args.Event);

        capture.ProcessRawMotion(rawValues.Pointer, mask.Pointer);

        Assert.Collection(
            events,
            x =>
            {
                Assert.Equal(InputEventType.MouseMove, x.Type);
                Assert.Equal(0, x.Code);
                Assert.Equal(1, x.Value);
            },
            y =>
            {
                Assert.Equal(InputEventType.MouseMove, y.Type);
                Assert.Equal(1, y.Code);
                Assert.Equal(-2, y.Value);
            },
            sync => Assert.Equal(InputEventType.Sync, sync.Type));
        Assert.Equal(events[0].Timestamp, events[1].Timestamp);
        Assert.Equal(events[0].Timestamp, events[2].Timestamp);
    }

    [Fact]
    public void RelativeCapture_WhenCaptureRestarts_DiscardsFractionalMotionFromPreviousRun()
    {
        using var fractionalValue = new UnmanagedDoubles(0.75, 0);
        using var wholeValue = new UnmanagedDoubles(0.5, 0);
        using var mask = new UnmanagedBytes(0b1);
        var capture = new TestX11RelativeCapture();
        var events = new List<CapturedInputEvent>();
        capture.InputReceived += (_, args) => events.Add(args.Event);

        capture.Started();
        capture.ProcessRawMotion(fractionalValue.Pointer, mask.Pointer);
        capture.Started();
        capture.ProcessRawMotion(wholeValue.Pointer, mask.Pointer);

        Assert.Empty(events);
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(null, null, false, true)]
    [InlineData(null, null, true, false)]
    public void ShouldUseLogicalCapture_SelectsXi2RootMotionForLogicalSemantics(
        bool? useAbsoluteCoordinates,
        bool? useLogicalCoordinates,
        bool legacyForceRelativeSetting,
        bool expected)
    {
        bool result = X11InputCapture.ShouldUseLogicalCapture(
            useAbsoluteCoordinates,
            useLogicalCoordinates,
            legacyForceRelativeSetting);

        Assert.Equal(expected, result);
    }

    private sealed class TestX11AbsoluteCapture(params (int X, int Y)[] positions) : X11AbsoluteCapture
    {
        private readonly Queue<(int X, int Y)> _positions = new(positions);

        internal void InitializePosition() => OnCaptureStarted();

        internal void ProcessRawMotion() => ProcessMotion(default);

        protected override bool TryGetPointerPosition(out int x, out int y)
        {
            var position = _positions.Dequeue();
            x = position.X;
            y = position.Y;
            return true;
        }
    }

    private sealed class TestX11RelativeCapture : X11RelativeCapture
    {
        internal void Started() => OnCaptureStarted();

        internal void ProcessRawMotion(IntPtr rawValues, IntPtr mask)
        {
            var rawEvent = new XIRawEvent
            {
                raw_values = rawValues,
                valuators = new XIValuatorState { mask = mask, mask_len = 1 },
            };
            IntPtr rawEventPointer = Marshal.AllocHGlobal(Marshal.SizeOf<XIRawEvent>());
            try
            {
                Marshal.StructureToPtr(rawEvent, rawEventPointer, fDeleteOld: false);
                ProcessMotion(new XGenericEventCookie { data = rawEventPointer });
            }
            finally
            {
                Marshal.FreeHGlobal(rawEventPointer);
            }
        }
    }

    private sealed class UnmanagedDoubles : IDisposable
    {
        internal IntPtr Pointer { get; }

        internal UnmanagedDoubles(params double[] values)
        {
            Pointer = Marshal.AllocHGlobal(values.Length * sizeof(double));
            for (int i = 0; i < values.Length; i++)
            {
                Marshal.WriteInt64(Pointer, i * sizeof(double), BitConverter.DoubleToInt64Bits(values[i]));
            }
        }

        public void Dispose() => Marshal.FreeHGlobal(Pointer);
    }

    private sealed class UnmanagedBytes : IDisposable
    {
        internal IntPtr Pointer { get; }

        internal UnmanagedBytes(byte value)
        {
            Pointer = Marshal.AllocHGlobal(1);
            Marshal.WriteByte(Pointer, value);
        }

        public void Dispose() => Marshal.FreeHGlobal(Pointer);
    }
}
