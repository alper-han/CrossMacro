using System;
using System.IO;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Infrastructure.Linux.Native.UInput;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Daemon.Services;

internal sealed class DaemonInputEventEncoder
{
    public void Write(BinaryWriter writer, UInputNative.input_event inputEvent)
    {
        writer.Write((byte)IpcOpCode.InputEvent);
        writer.Write(GetEventType(inputEvent.type, inputEvent.code));
        writer.Write((int)inputEvent.code);
        writer.Write(inputEvent.value);
        writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
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
            if (code is UInputNative.REL_WHEEL or UInputNative.REL_HWHEEL)
            {
                return (byte)InputEventType.MouseScroll;
            }

            return (byte)InputEventType.MouseMove;
        }

        if (type == UInputNative.EV_ABS)
        {
            if (code == UInputNative.ABS_X || code == UInputNative.ABS_Y)
            {
                return (byte)InputEventType.MouseMove;
            }
        }

        if (type == UInputNative.EV_SYN)
        {
            return (byte)InputEventType.Sync;
        }

        return (byte)InputEventType.Unknown;
    }
}
