namespace CrossMacro.Platform.Abstractions;

public interface IDisplayEnvironmentDiagnostic
{
    public string? XdgSessionType { get; }
    public string? Display { get; }
    public string? WaylandDisplay { get; }
}
