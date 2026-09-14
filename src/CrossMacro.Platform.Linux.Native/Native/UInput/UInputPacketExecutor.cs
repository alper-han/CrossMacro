namespace CrossMacro.Platform.Linux.Native.UInput;

/// <summary>Executes packet reassertion without owning the native device or readiness lifecycle.</summary>
internal sealed class UInputPacketExecutor(int width, int height)
{
    private readonly UInputAbsolutePacketState _absolutePacketState = new(width, height);

    public void Send(ushort type, ushort code, int value, IUInputEventWriter writer)
    {
        if (type is UInputNative.EV_SYN && code is UInputNative.SYN_REPORT)
        {
            var plan = _absolutePacketState.CompletePacket();
            if (plan?.Reassertion is { } reassertion)
            {
                WriteAbsolutePosition(reassertion, writer);
                WriteAbsolutePosition(plan.Value.Target, writer);
                return;
            }

            writer.WriteEvent(type, code, value);
            return;
        }

        writer.WriteEvent(type, code, value);
        _absolutePacketState.Observe(type, code, value);
    }

    private static void WriteAbsolutePosition((int X, int Y) position, IUInputEventWriter writer)
    {
        writer.WriteEvent(UInputNative.EV_ABS, UInputNative.ABS_X, position.X);
        writer.WriteEvent(UInputNative.EV_ABS, UInputNative.ABS_Y, position.Y);
        writer.WriteEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
    }

}
