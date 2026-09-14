namespace CrossMacro.Platform.Abstractions.Runtime;

public interface IDisplayEnvironmentDiagnostic
{
    public string? XdgSessionType { get; }
    public string? Display { get; }
    public string? WaylandDisplay { get; }
}
