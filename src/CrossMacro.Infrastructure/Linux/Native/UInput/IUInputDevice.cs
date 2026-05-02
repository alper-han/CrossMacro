using System;

namespace CrossMacro.Infrastructure.Linux.Native.UInput;

public interface IUInputDevice : IDisposable
{
    bool SupportsAbsoluteCoordinates { get; }

    void CreateVirtualInputDevice();

    void Move(int dx, int dy);

    void MoveAbsolute(int x, int y);

    void EmitButton(int buttonCode, bool pressed);

    void EmitKey(int keyCode, bool pressed);

    void SendEvent(ushort type, ushort code, int value);
}
