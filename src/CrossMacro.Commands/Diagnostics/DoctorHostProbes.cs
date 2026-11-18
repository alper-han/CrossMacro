namespace CrossMacro.Commands.Diagnostics;

/// <summary>The operating-system facts the doctor reads, grouped so tests can supply a fake host.</summary>
internal sealed record DoctorHostProbes(
    Func<string, string?> GetEnvironmentVariable,
    Func<bool> IsLinux,
    Func<bool> IsMacOS)
{
    public static DoctorHostProbes Current { get; } = new(
        Environment.GetEnvironmentVariable,
        OperatingSystem.IsLinux,
        OperatingSystem.IsMacOS);
}
