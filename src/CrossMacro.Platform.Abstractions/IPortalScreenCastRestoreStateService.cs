namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Provides status and reset operations for platform screen-cast restore state.
/// </summary>
public interface IPortalScreenCastRestoreStateService
{
    public Task<bool> HasRestoreStateAsync(CancellationToken cancellationToken);

    public Task ClearRestoreStateAsync(CancellationToken cancellationToken);
}
