using System;
using System.Threading;

namespace CrossMacro.Core.Services.Playback;

public class PlaybackStateManager : IDisposable
{
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private CancellationTokenSource? _cts;
    private bool _disposed;
    
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    
    public event EventHandler<bool>? PlayingChanged;
    
    public event EventHandler<bool>? PausedChanged;
    
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
    
    public void Stop()
    {
        _cts?.Cancel();
        IsPlaying = false;
        IsPaused = false;
        _pauseEvent.Set(); 
        
        PlayingChanged?.Invoke(this, false);
    }
    
    public void Pause()
    {
        if (!IsPlaying || IsPaused)
            return;
            
        IsPaused = true;
        _pauseEvent.Reset();
        
        PausedChanged?.Invoke(this, true);
    }
    
    public void Resume()
    {
        if (!IsPlaying || !IsPaused)
            return;
            
        IsPaused = false;
        _pauseEvent.Set();
        
        PausedChanged?.Invoke(this, false);
    }
    
    public void WaitIfPaused(CancellationToken cancellationToken)
    {
        if (IsPaused)
        {
            _pauseEvent.Wait(cancellationToken);
        }
    }
    
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
