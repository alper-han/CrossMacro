namespace CrossMacro.Platform.Linux.DisplayServer;

/// <summary>
/// Provides the logical output rectangles that make up the Linux desktop.
/// </summary>
internal interface IOutputTopologyProvider
{
    public Task<IReadOnlyList<ScreenRect>> GetOutputBoundsAsync(
        CancellationToken cancellationToken = default);
}
