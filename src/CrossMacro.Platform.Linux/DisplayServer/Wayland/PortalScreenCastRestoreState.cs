namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed record PortalScreenCastRestoreState
{
    public static PortalScreenCastRestoreState Empty { get; } = new();

    public string? RestoreToken { get; init; }

    public string? RestoreData { get; init; }

    public string? Context { get; init; }

    public bool HasRestoreState => !string.IsNullOrWhiteSpace(RestoreToken) || !string.IsNullOrWhiteSpace(RestoreData);
}
