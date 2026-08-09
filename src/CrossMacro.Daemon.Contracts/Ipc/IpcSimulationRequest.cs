
namespace CrossMacro.Daemon.Contracts.Ipc;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcSimulationRequest : IEquatable<IpcSimulationRequest>
{
    public ushort Type;
    public ushort Code;
    public int Value;
    public long DelayAfterMicroseconds;

    public readonly bool Equals(IpcSimulationRequest other) =>
        Type == other.Type
        && Code == other.Code
        && Value == other.Value
        && DelayAfterMicroseconds == other.DelayAfterMicroseconds;

    public override readonly bool Equals([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj) => obj is IpcSimulationRequest other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Type, Code, Value, DelayAfterMicroseconds);

    public static bool operator ==(IpcSimulationRequest left, IpcSimulationRequest right) => left.Equals(right);

    public static bool operator !=(IpcSimulationRequest left, IpcSimulationRequest right) => !left.Equals(right);
}
