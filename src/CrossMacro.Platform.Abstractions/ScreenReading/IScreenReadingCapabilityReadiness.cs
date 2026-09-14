namespace CrossMacro.Platform.Abstractions.ScreenReading;

/// <summary>
/// Ensures that asynchronous screen-reading backend discovery has completed before
/// a capability-dependent decision is made.
/// </summary>
public interface IScreenReadingCapabilityReadiness
{
    public Task EnsureReadyAsync(CancellationToken cancellationToken = default);
}
