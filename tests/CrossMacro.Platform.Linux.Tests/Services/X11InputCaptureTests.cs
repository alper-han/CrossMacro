namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class X11InputCaptureTests
{
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
}
