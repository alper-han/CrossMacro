using System;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

/// <summary>
/// Service for handling text expansion feature
/// </summary>
public interface ITextExpansionService : IDisposable
{
    /// <summary>
    /// Starts the text expansion service monitoring
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the text expansion service monitoring
    /// </summary>
    void Stop();

    /// <summary>
    /// Check if the service is currently running
    /// </summary>
    bool IsRunning { get; }
}
