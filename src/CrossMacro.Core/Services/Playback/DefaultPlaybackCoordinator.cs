using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using Serilog;

namespace CrossMacro.Core.Services.Playback;

/// <summary>
/// Default playback coordinator implementation.
/// Handles Corner Reset for relative mode and position sync for absolute mode.
/// </summary>
public class DefaultPlaybackCoordinator : IPlaybackCoordinator
{
    private readonly IMousePositionProvider? _positionProvider;
    private readonly bool _preferRelativeForAbsoluteMoves;
    
    public int CurrentX { get; private set; }
    public int CurrentY { get; private set; }
    
    public DefaultPlaybackCoordinator(
        IMousePositionProvider? positionProvider = null,
        bool preferRelativeForAbsoluteMoves = true)
    {
        _positionProvider = positionProvider;
        _preferRelativeForAbsoluteMoves = preferRelativeForAbsoluteMoves;
    }
    
    public void UpdatePosition(int x, int y)
    {
        CurrentX = x;
        CurrentY = y;
    }
    
    public void AddDelta(int dx, int dy)
    {
        CurrentX += dx;
        CurrentY += dy;
    }

    public async Task InitializeAsync(
        MacroSequence macro, 
        IInputSimulator simulator, 
        int screenWidth, 
        int screenHeight,
        CancellationToken cancellationToken)
    {
        // Reset position
        CurrentX = 0;
        CurrentY = 0;
        
        // Try to get current position from provider
        if (_positionProvider != null && _positionProvider.IsSupported)
        {
            try
            {
                var pos = await _positionProvider.GetAbsolutePositionAsync();
                if (pos.HasValue)
                {
                    CurrentX = pos.Value.X;
                    CurrentY = pos.Value.Y;
                    Log.Information("[PlaybackCoordinator] Position initialized from provider: ({X}, {Y})", CurrentX, CurrentY);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[PlaybackCoordinator] Failed to get initial position from provider");
            }
        }
        
        // Find first mouse event to determine start position
        var firstMouseEvent = macro.Events.FirstOrDefault(e =>
            e.Type == EventType.MouseMove ||
            e.Type == EventType.ButtonPress ||
            e.Type == EventType.ButtonRelease ||
            e.Type == EventType.Click);

        if (firstMouseEvent.Type == EventType.None)
        {
            Log.Information("[PlaybackCoordinator] No mouse events found in macro, skipping start position move");
            return;
        }

        if (macro.IsAbsoluteCoordinates)
        {
            await InitializeAbsoluteModeAsync(firstMouseEvent, simulator, screenWidth, screenHeight, cancellationToken);
        }
        else
        {
            await InitializeRelativeModeAsync(macro, simulator, cancellationToken);
        }
    }

    private async Task InitializeAbsoluteModeAsync(
        MacroEvent firstEvent,
        IInputSimulator simulator,
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken)
    {
        if (screenWidth > 0 && screenHeight > 0)
        {
            int startX = Math.Clamp(firstEvent.X, 0, screenWidth);
            int startY = Math.Clamp(firstEvent.Y, 0, screenHeight);

            int dx = startX - CurrentX;
            int dy = startY - CurrentY;

            if (dx != 0 || dy != 0)
            {
                if (_preferRelativeForAbsoluteMoves)
                {
                    Log.Information("[PlaybackCoordinator] Moving to start position: ({X}, {Y}) via relative delta ({DX}, {DY})",
                        startX, startY, dx, dy);
                    simulator.MoveRelative(dx, dy);
                }
                else
                {
                    Log.Information("[PlaybackCoordinator] Moving to start position: ({X}, {Y}) via absolute move",
                        startX, startY);
                    simulator.MoveAbsolute(startX, startY);
                }
            }
            CurrentX = startX;
            CurrentY = startY;
        }
        else
        {
            // Blind mode: Corner Reset to sync
            Log.Information("[PlaybackCoordinator] Blind Mode: Performing Corner Reset (Force 0,0)...");

            for (int r = 0; r < 5; r++)
            {
                simulator.MoveRelative(-10000, -10000);
                await Task.Delay(20, cancellationToken);
            }

            await Task.Delay(100, cancellationToken);
            CurrentX = 0;
            CurrentY = 0;
        }
    }

    private async Task InitializeRelativeModeAsync(
        MacroSequence macro, 
        IInputSimulator simulator, 
        CancellationToken cancellationToken)
    {
        if (!macro.SkipInitialZeroZero)
        {
            // Recording did Corner Reset, so we should too
            Log.Information("[PlaybackCoordinator] Relative mode: Performing Corner Reset (0,0)...");
            simulator.MoveRelative(-20000, -20000);
            await Task.Delay(10, cancellationToken);
            CurrentX = 0;
            CurrentY = 0;
        }
        else
        {
            // Recording started from wherever cursor was
            Log.Information("[PlaybackCoordinator] Relative mode: starting from current position");
        }
    }

    public async Task PrepareIterationAsync(
        int iteration,
        MacroSequence macro,
        IInputSimulator simulator,
        int screenWidth,
        int screenHeight,
        CancellationToken cancellationToken)
    {
        // First iteration is handled by InitializeAsync
        if (iteration == 0)
            return;

        if (macro.IsAbsoluteCoordinates && screenWidth > 0 && screenHeight > 0)
        {
            // Sync position from provider to prevent desync between tracked and actual cursor position
            if (_positionProvider != null && _positionProvider.IsSupported)
            {
                try
                {
                    var pos = await _positionProvider.GetAbsolutePositionAsync();
                    if (pos.HasValue)
                    {
                        CurrentX = pos.Value.X;
                        CurrentY = pos.Value.Y;
                        Log.Debug("[PlaybackCoordinator] Iteration {I}: Position synced ({X}, {Y})",
                            iteration + 1, CurrentX, CurrentY);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[PlaybackCoordinator] Failed to sync position from provider");
                }
            }

            var firstEvent = macro.Events.FirstOrDefault(e => e.Type == EventType.MouseMove);
            if (firstEvent.Type != EventType.None)
            {
                int startX = Math.Clamp(firstEvent.X, 0, screenWidth);
                int startY = Math.Clamp(firstEvent.Y, 0, screenHeight);

                int dx = startX - CurrentX;
                int dy = startY - CurrentY;

                if (dx != 0 || dy != 0)
                {
                    if (_preferRelativeForAbsoluteMoves)
                    {
                        simulator.MoveRelative(dx, dy);
                    }
                    else
                    {
                        simulator.MoveAbsolute(startX, startY);
                    }
                }
                CurrentX = startX;
                CurrentY = startY;
            }
        }
        else if (!macro.IsAbsoluteCoordinates && !macro.SkipInitialZeroZero)
        {
            // Relative mode with Corner Reset
            Log.Information("[PlaybackCoordinator] Iteration {I}: Performing Corner Reset (0,0)", iteration + 1);
            simulator.MoveRelative(-20000, -20000);
            await Task.Delay(10, cancellationToken);
            CurrentX = 0;
            CurrentY = 0;
        }
        // If SkipInitialZeroZero=true, just continue from current position
    }
}
