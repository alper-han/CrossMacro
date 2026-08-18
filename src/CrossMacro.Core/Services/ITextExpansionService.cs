
namespace CrossMacro.Core.Services;

/// <summary>
/// Service for handling text expansion feature
/// </summary>
public interface ITextExpansionService : IDisposable
{
    /// <summary>
    /// Starts the text expansion service monitoring
    /// </summary>
    public void Start();

    /// <summary>
    /// Starts the text expansion service monitoring asynchronously.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the text expansion service monitoring
    /// </summary>
    public void StopExpansion();

    public Task StopExpansionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the service is currently running
    /// </summary>
    public bool IsRunning { get; }
}
