namespace CrossMacro.Platform.Abstractions.Setup;

/// <summary>
/// Provides temporary direct input access when a daemon-backed Linux installation
/// cannot use either the daemon or the direct capture fallback.
/// </summary>
public interface ILinuxDirectInputQuickSetupService
{
    public ValueTask<bool> ShouldPromptAsync(CancellationToken cancellationToken = default);

    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default);
}
