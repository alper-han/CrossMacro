
namespace CrossMacro.Daemon.Services;

internal static class DaemonInputEventEncoder
{
    public static void Write(BinaryWriter writer, UInputNative.input_event inputEvent)
    {
        writer.Write((byte)IpcOpCode.InputEvent);
        writer.Write(GetEventType(inputEvent.type, inputEvent.code));
        writer.Write((int)inputEvent.code);
        writer.Write(inputEvent.value);
        writer.Write(GetMonotonicTimestampMicroseconds(inputEvent));
    }

    private static byte GetEventType(ushort type, ushort code)
    {
        if (type == UInputNative.EV_KEY)
        {
            if (UInputNative.IsMouseButton(code))
            {
                return (byte)InputEventType.MouseButton;
            }

            return (byte)InputEventType.Key;
        }

        if (type == UInputNative.EV_REL)
        {
            if (code is UInputNative.REL_WHEEL
                or UInputNative.REL_HWHEEL
                or UInputNative.REL_WHEEL_HI_RES
                or UInputNative.REL_HWHEEL_HI_RES)
            {
                return (byte)InputEventType.MouseScroll;
            }

            return (byte)InputEventType.MouseMove;
        }

        if (type == UInputNative.EV_ABS && code is UInputNative.ABS_X or UInputNative.ABS_Y)
        {
            return (byte)InputEventType.MouseMove;
        }

        if (type == UInputNative.EV_SYN)
        {
            return (byte)InputEventType.Sync;
        }

        return (byte)InputEventType.Unknown;
    }

    private static long GetMonotonicTimestampMicroseconds(UInputNative.input_event inputEvent)
    {
        var seconds = inputEvent.time_sec.ToInt64();
        var microseconds = inputEvent.time_usec.ToInt64();
        if ((seconds > 0 || microseconds > 0)
            && seconds >= 0
            && (microseconds is >= 0 and < 1_000_000))
        {
            try
            {
                return checked((seconds * 1_000_000) + microseconds);
            }
            catch (OverflowException)
            {
                // Fall back to the process monotonic clock below.
            }
        }

        return Math.Max(1, Stopwatch.GetTimestamp() * 1_000_000 / Stopwatch.Frequency);
    }
}
