
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
    /// Stops the text expansion service monitoring
    /// </summary>
    public void StopExpansion();

    /// <summary>
    /// Check if the service is currently running
    /// </summary>
    public bool IsRunning { get; }
}
