
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Default playback coordinator implementation.
/// Handles Corner Reset for relative mode and position sync for absolute mode.
/// </summary>
public class DefaultPlaybackCoordinator(IMousePositionProvider? positionProvider = null) : IPlaybackCoordinator
{
    private readonly IMousePositionProvider? _positionProvider = positionProvider;
    public int CurrentX { get; private set; }
    public int CurrentY { get; private set; }

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
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(simulator);
        // Reset position
        CurrentX = 0;
        CurrentY = 0;

        // Try to get current position from provider
        if (_positionProvider is not null && _positionProvider.IsSupported)
        {
            try
            {
                var pos = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
                if (pos is not null)
                {
                    CurrentX = pos.Value.X;
                    CurrentY = pos.Value.Y;
                    Log.Information("[PlaybackCoordinator] Position initialized from provider: ({X}, {Y})", CurrentX, CurrentY);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[PlaybackCoordinator] Failed to get initial position from provider");
            }
        }

        var firstPositionRelevantMouseEvent = FindFirstPositionRelevantMouseEvent(macro);
        var firstCoordinateMode = firstPositionRelevantMouseEvent.Type is EventType.None
            ? null
            : MacroPositionSemantics.ResolveCoordinateMode(firstPositionRelevantMouseEvent, macro.IsAbsoluteCoordinates);

        if (firstCoordinateMode is MouseCoordinateMode.Absolute)
        {
            Log.Information("[PlaybackCoordinator] Absolute mode: first coordinate-bearing event will establish playback position");
        }
        else if (firstCoordinateMode is MouseCoordinateMode.Relative)
        {
            await InitializeRelativeModeAsync(macro, simulator, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Log.Information("[PlaybackCoordinator] No coordinate-bearing mouse event found in macro, skipping start position move");
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
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
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
        ArgumentNullException.ThrowIfNull(macro);
        ArgumentNullException.ThrowIfNull(simulator);
        // First iteration is handled by InitializeAsync
        if (iteration is 0)
        {
            return;
        }

        var firstPositionRelevantMouseEvent = FindFirstPositionRelevantMouseEvent(macro);
        var firstCoordinateMode = firstPositionRelevantMouseEvent.Type is EventType.None
            ? null
            : MacroPositionSemantics.ResolveCoordinateMode(firstPositionRelevantMouseEvent, macro.IsAbsoluteCoordinates);

        if (firstCoordinateMode is MouseCoordinateMode.Absolute)
        {
            // Sync tracked position when possible; the first absolute event itself performs the movement.
            if (_positionProvider is not null && _positionProvider.IsSupported)
            {
                try
                {
                    var pos = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
                    if (pos is not null)
                    {
                        CurrentX = pos.Value.X;
                        CurrentY = pos.Value.Y;
                        Log.Debug("[PlaybackCoordinator] Iteration {I}: Position synced ({X}, {Y})",
                            iteration + 1, CurrentX, CurrentY);
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Warning(ex, "[PlaybackCoordinator] Failed to sync position from provider");
                }
            }
        }
        else if (firstCoordinateMode is MouseCoordinateMode.Relative
&& !macro.SkipInitialZeroZero)
        {
            // Relative mode with Corner Reset
            Log.Information("[PlaybackCoordinator] Iteration {I}: Performing Corner Reset (0,0)", iteration + 1);
            simulator.MoveRelative(-20000, -20000);
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            CurrentX = 0;
            CurrentY = 0;
        }
        // If SkipInitialZeroZero=true, just continue from current position
    }

    private static MacroEvent FindFirstPositionRelevantMouseEvent(MacroSequence macro)
    {
        return macro.Events.FirstOrDefault(e =>
            e.Type is EventType.MouseMove
|| MacroPositionSemantics.IsNonScrollMouseButtonEvent(e));
    }
}
