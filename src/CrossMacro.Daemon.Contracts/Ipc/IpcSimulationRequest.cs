
namespace CrossMacro.Daemon.Contracts.Ipc;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcSimulationRequest : IEquatable<IpcSimulationRequest>
{
    public ushort Type;
    public ushort Code;
    public int Value;
    public int DelayAfterMs;

    public readonly bool Equals(IpcSimulationRequest other) =>
        Type == other.Type
        && Code == other.Code
        && Value == other.Value
        && DelayAfterMs == other.DelayAfterMs;

    public override readonly bool Equals([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] object? obj) => obj is IpcSimulationRequest other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Type, Code, Value, DelayAfterMs);

    public static bool operator ==(IpcSimulationRequest left, IpcSimulationRequest right) => left.Equals(right);

    public static bool operator !=(IpcSimulationRequest left, IpcSimulationRequest right) => !left.Equals(right);
}
