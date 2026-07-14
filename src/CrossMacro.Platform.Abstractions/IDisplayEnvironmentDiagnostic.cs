namespace CrossMacro.Platform.Abstractions;

public interface IDisplayEnvironmentDiagnostic
{
    string? XdgSessionType { get; }
    string? Display { get; }
    string? WaylandDisplay { get; }
}
