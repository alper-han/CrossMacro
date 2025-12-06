using System;
using System.Threading;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Manages playback state (play, pause, resume, stop)
/// Single Responsibility: Only handles state transitions and pause synchronization
/// </summary>
public class PlaybackStateManager : IDisposable
{
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private CancellationTokenSource? _cts;
    private bool _disposed;
    
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    
    /// <summary>
    /// Event fired when playback state changes
    /// </summary>
    public event EventHandler<bool>? PlayingChanged;
    
    /// <summary>
    /// Event fired when pause state changes
    /// </summary>
    public event EventHandler<bool>? PausedChanged;
    
    /// <summary>
    /// Start playback
    /// </summary>
    public CancellationToken Start(CancellationToken externalToken = default)
    {
        if (IsPlaying)
            throw new InvalidOperationException("Playback already in progress");
            
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        IsPlaying = true;
        IsPaused = false;
        _pauseEvent.Set();
        
        PlayingChanged?.Invoke(this, true);
        
        return _cts.Token;
    }
    
    /// <summary>
    /// Stop playback
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        IsPlaying = false;
        IsPaused = false;
        _pauseEvent.Set(); // Ensure not blocked
        
        PlayingChanged?.Invoke(this, false);
    }
    
    /// <summary>
    /// Pause playback
    /// </summary>
    public void Pause()
    {
        if (!IsPlaying || IsPaused)
            return;
            
        IsPaused = true;
        _pauseEvent.Reset();
        
        PausedChanged?.Invoke(this, true);
    }
    
    /// <summary>
    /// Resume playback
    /// </summary>
    public void Resume()
    {
        if (!IsPlaying || !IsPaused)
            return;
            
        IsPaused = false;
        _pauseEvent.Set();
        
        PausedChanged?.Invoke(this, false);
    }
    
    /// <summary>
    /// Wait if paused (for use in playback loop)
    /// </summary>
    public void WaitIfPaused(CancellationToken cancellationToken)
    {
        if (IsPaused)
        {
            _pauseEvent.Wait(cancellationToken);
        }
    }
    
    /// <summary>
    /// Mark playback as finished (called when playback completes naturally)
    /// </summary>
    public void Finish()
    {
        IsPlaying = false;
        IsPaused = false;
        _pauseEvent.Set();
        
        _cts?.Dispose();
        _cts = null;
        
        PlayingChanged?.Invoke(this, false);
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        _cts?.Dispose();
        _pauseEvent.Dispose();
    }
}
