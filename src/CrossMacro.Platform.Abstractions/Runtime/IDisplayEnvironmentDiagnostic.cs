namespace CrossMacro.Platform.Abstractions.Runtime;

public interface IDisplayEnvironmentDiagnostic
{
    public string? Display { get; }
    public string? WaylandDisplay { get; }
}
