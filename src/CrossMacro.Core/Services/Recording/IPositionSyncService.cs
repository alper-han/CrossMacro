using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services.Recording;

/// <summary>
/// Interface for mouse position synchronization service
/// Single Responsibility: Handles background cursor position syncing to correct drift
/// </summary>
public interface IPositionSyncService : IDisposable
{
    /// <summary>
    /// Start background position synchronization
    /// </summary>
    /// <param name="onPositionChanged">Callback when position changes significantly</param>
    /// <param name="getCurrentPosition">Function to get current cached position</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAsync(
        Action<int, int, long> onPositionChanged,
        Func<(int X, int Y)> getCurrentPosition,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Stop position synchronization
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Whether sync is currently running
    /// </summary>
    bool IsRunning { get; }
}
