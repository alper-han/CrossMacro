
namespace CrossMacro.Platform.Linux.Native.UInput;

public interface IUInputDevice : IDisposable
{
    public bool SupportsAbsoluteCoordinates { get; }

    public void CreateVirtualInputDevice();

    public Task CreateVirtualInputDeviceAsync(CancellationToken cancellationToken = default);

    public void Move(int dx, int dy);

    public void MoveAbsolute(int x, int y);

    public void EmitButton(int buttonCode, bool pressed);

    public void EmitKey(int keyCode, bool pressed);

    public void SendEvent(ushort type, ushort code, int value);
}
