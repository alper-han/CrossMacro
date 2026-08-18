namespace CrossMacro.Platform.Linux.Tests.Native.UInput;

public sealed class UInputAbsolutePacketStateTests
{
    [Fact]
    public void CompletePacket_WhenFirstTargetIsInitialOrigin_ReturnsReassertionPlan()
    {
        var state = new UInputAbsolutePacketState(1920, 1080);

        ObserveAbsoluteMove(state, 0, 0);
        var plan = state.CompletePacket();

        Assert.Equal((0, 0), plan?.Target);
        Assert.Equal((1, 0), plan?.Reassertion);
    }

    [Fact]
    public void CompletePacket_WhenPureAbsoluteTargetRepeats_ReturnsReassertionPlan()
    {
        var state = new UInputAbsolutePacketState(1920, 1080);
        ObserveAbsoluteMove(state, 100, 200);
        _ = state.CompletePacket();

        ObserveAbsoluteMove(state, 100, 200);
        var plan = state.CompletePacket();

        Assert.Equal((100, 200), plan?.Target);
        Assert.Equal((101, 200), plan?.Reassertion);
    }

    [Fact]
    public void CompletePacket_WhenAbsoluteTargetChanges_DoesNotReassert()
    {
        var state = new UInputAbsolutePacketState(1920, 1080);

        ObserveAbsoluteMove(state, 100, 200);
        var plan = state.CompletePacket();

        Assert.Equal((100, 200), plan?.Target);
        Assert.Null(plan?.Reassertion);
    }

    [Fact]
    public void CompletePacket_WhenPacketMixesInputTypes_DoesNotRewritePacket()
    {
        var state = new UInputAbsolutePacketState(1920, 1080);
        ObserveAbsoluteMove(state, 100, 200);
        _ = state.CompletePacket();
        state.Observe(UInputNative.EV_ABS, UInputNative.ABS_X, 100);
        state.Observe(UInputNative.EV_ABS, UInputNative.ABS_Y, 200);
        state.Observe(UInputNative.EV_KEY, UInputNative.BTN_LEFT, 1);

        var plan = state.CompletePacket();

        Assert.Equal((100, 200), plan?.Target);
        Assert.Null(plan?.Reassertion);
    }

    [Fact]
    public void CompletePacket_WhenRawTargetRepeatsOutsideBounds_ReassertsTheKernelClampedTarget()
    {
        var state = new UInputAbsolutePacketState(3, 2);
        ObserveAbsoluteMove(state, 99, -5);

        var first = state.CompletePacket();
        ObserveAbsoluteMove(state, 99, -5);
        var repeated = state.CompletePacket();

        Assert.Equal((2, 0), first?.Target);
        Assert.Null(first?.Reassertion);
        Assert.Equal((2, 0), repeated?.Target);
        Assert.Equal((1, 0), repeated?.Reassertion);
    }

    private static void ObserveAbsoluteMove(UInputAbsolutePacketState state, int x, int y)
    {
        state.Observe(UInputNative.EV_ABS, UInputNative.ABS_X, x);
        state.Observe(UInputNative.EV_ABS, UInputNative.ABS_Y, y);
    }
}
