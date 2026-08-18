
namespace CrossMacro.Daemon.Contracts.Ipc;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcInputEvent : IEquatable<IpcInputEvent>
{
    public byte Type;
    public int Code;
    public int Value;
    public long Timestamp;

    public readonly bool Equals(IpcInputEvent other) =>
        Type == other.Type
        && Code == other.Code
        && Value == other.Value
        && Timestamp == other.Timestamp;

    public override readonly bool Equals([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj) => obj is IpcInputEvent other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Type, Code, Value, Timestamp);

    public static bool operator ==(IpcInputEvent left, IpcInputEvent right) => left.Equals(right);

    public static bool operator !=(IpcInputEvent left, IpcInputEvent right) => !left.Equals(right);
}
