
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Default playback coordinator implementation.
/// Handles Corner Reset for relative mode and position sync for absolute mode.
/// </summary>
public class DefaultPlaybackCoordinator(IMousePositionProvider? positionProvider = null) : IPlaybackCoordinator
{
    private static readonly TimeSpan CornerPositionSettleTimeout = TimeSpan.FromMilliseconds(250);
    private const int CornerPositionTolerance = 1;
    private static readonly TimeSpan RawMovementPositionRefreshInterval = TimeSpan.FromMilliseconds(4);
    private const int RawMovementPositionRefreshAttempts = 5;
    private const int RawMovementMinimumRefreshAttemptsWithoutReference = 3;

    private readonly IMousePositionProvider? _positionProvider = positionProvider;
    public int CurrentX { get; private set; }
    public int CurrentY { get; private set; }
    public bool HasKnownPosition { get; private set; }
    private ScreenRect? _desktopBounds;
    private (int X, int Y)? _positionBeforeRawMovement;
    private bool _rawMovementMayBePending;

    public void ConfigureDesktopBounds(ScreenRect? desktopBounds)
    {
        _desktopBounds = desktopBounds;
    }

    public void UpdatePosition(int x, int y)
    {
        CurrentX = x;
        CurrentY = y;
        HasKnownPosition = true;
        _positionBeforeRawMovement = null;
        _rawMovementMayBePending = false;
    }

    public void InvalidatePosition(bool movementMayBePending = false)
    {
        if (movementMayBePending)
        {
            if (HasKnownPosition)
            {
                _positionBeforeRawMovement = (CurrentX, CurrentY);
            }

            _rawMovementMayBePending = true;
        }
        else
        {
            _positionBeforeRawMovement = null;
            _rawMovementMayBePending = false;
        }

        HasKnownPosition = false;
    }

    public async Task<bool> TrySynchronizePositionAsync(CancellationToken cancellationToken)
    {
        if (HasKnownPosition)
        {
            return true;
        }

        if (_positionProvider is null || !_positionProvider.SupportsAbsolutePosition)
        {
            return false;
        }

        var position = _rawMovementMayBePending
            ? await SynchronizeAfterRawMovementAsync(cancellationToken).ConfigureAwait(false)
            : await QueryPositionAsync(cancellationToken).ConfigureAwait(false);
        if (position is null)
        {
            return false;
        }

        UpdatePosition(position.Value.X, position.Value.Y);
        return true;
    }

    public async Task<bool> RefreshPositionAsync(CancellationToken cancellationToken)
    {
        var position = await QueryPositionAsync(cancellationToken).ConfigureAwait(false);
        if (position is null)
        {
            return false;
        }

        UpdatePosition(position.Value.X, position.Value.Y);
        return true;
    }

    public async Task<bool> WaitForPositionAsync(int expectedX, int expectedY, CancellationToken cancellationToken)
    {
        var result = await AbsoluteCursorPositionSynchronizer.WaitAsync(
            _positionProvider,
            expectedX,
            expectedY,
            cancellationToken).ConfigureAwait(false);
        if (result.IsSettled)
        {
            return true;
        }

        if (result.LastObservedPosition is { } observedPosition)
        {
            Log.Warning(
                "[PlaybackCoordinator] Absolute cursor move did not settle at ({ExpectedX},{ExpectedY}); last observed position is ({ObservedX},{ObservedY}).",
                expectedX,
                expectedY,
                observedPosition.X,
                observedPosition.Y);
        }
        else
        {
            Log.Warning(
                "[PlaybackCoordinator] Absolute cursor move did not settle at ({ExpectedX},{ExpectedY}); no cursor position was observed.",
                expectedX,
                expectedY);
        }

        return false;
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
        HasKnownPosition = false;
        _positionBeforeRawMovement = null;
        _rawMovementMayBePending = false;

        // Try to get current position from provider
        if (_positionProvider is not null
            && _positionProvider.HasUsableAbsolutePosition()
            && await TrySynchronizePositionAsync(cancellationToken).ConfigureAwait(false))
        {
            Log.Information("[PlaybackCoordinator] Position initialized from provider: ({X}, {Y})", CurrentX, CurrentY);
        }

        var firstCoordinateMode = MacroPositionSemantics.ResolveInitialCoordinateMode(macro);

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
            Log.Information("[PlaybackCoordinator] Relative mode: performing desktop corner reset...");
            var previousPosition = GetTrackedPosition();
            var expectedPosition = MouseCornerReset.MoveToDesktopOrigin(simulator, _desktopBounds);
            await SynchronizeAfterCornerResetAsync(
                previousPosition,
                expectedPosition,
                cancellationToken).ConfigureAwait(false);
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

        var firstCoordinateMode = MacroPositionSemantics.ResolveInitialCoordinateMode(macro);

        if (firstCoordinateMode is MouseCoordinateMode.Absolute)
        {
            // Sync tracked position when possible; the first absolute event itself performs the movement.
            if (_positionProvider is not null && _positionProvider.HasUsableAbsolutePosition())
            {
                InvalidatePosition();
                if (await TrySynchronizePositionAsync(cancellationToken).ConfigureAwait(false))
                {
                    Log.Debug("[PlaybackCoordinator] Iteration {I}: Position synced ({X}, {Y})",
                        iteration + 1, CurrentX, CurrentY);
                }
            }
        }
        else if (firstCoordinateMode is MouseCoordinateMode.Relative
            && !macro.SkipInitialZeroZero)
        {
            Log.Information("[PlaybackCoordinator] Iteration {I}: performing desktop corner reset", iteration + 1);
            var previousPosition = GetTrackedPosition();
            var expectedPosition = MouseCornerReset.MoveToDesktopOrigin(simulator, _desktopBounds);
            await SynchronizeAfterCornerResetAsync(
                previousPosition,
                expectedPosition,
                cancellationToken).ConfigureAwait(false);
        }
        // If SkipInitialZeroZero=true, just continue from current position
    }

    private async Task SynchronizeAfterCornerResetAsync(
        (int X, int Y)? previousPosition,
        (int X, int Y)? expectedPosition,
        CancellationToken cancellationToken)
    {
        InvalidatePosition();
        var result = await AbsoluteCursorPositionSynchronizer.WaitUntilAsync(
            _positionProvider,
            position => IsCornerResetPosition(position, previousPosition, expectedPosition),
            CornerPositionSettleTimeout,
            cancellationToken).ConfigureAwait(false);

        if (result.IsSettled)
        {
            if (result.LastObservedPosition is { } settledPosition)
            {
                UpdatePosition(settledPosition.X, settledPosition.Y);
            }
            else
            {
                UpdateUnobservedCornerPosition(expectedPosition);
            }

            return;
        }

        if (expectedPosition is not null || _desktopBounds is not null)
        {
            UpdateUnobservedCornerPosition(expectedPosition);
        }
        else if (result.LastObservedPosition is { } observedPosition)
        {
            Log.Warning(
                "[PlaybackCoordinator] Corner reset settled at ({X}, {Y}) instead of the requested desktop origin",
                observedPosition.X,
                observedPosition.Y);
            UpdatePosition(observedPosition.X, observedPosition.Y);
        }
        else
        {
            UpdateUnobservedCornerPosition(expectedPosition);
        }
    }

    private bool IsCornerResetPosition(
        (int X, int Y) position,
        (int X, int Y)? previousPosition,
        (int X, int Y)? expectedPosition)
    {
        if (expectedPosition is { } expected)
        {
            return IsWithinCornerTolerance(position, expected);
        }

        return IsPlausibleDesktopCorner(position)
            || (_desktopBounds is null && (previousPosition is null || position != previousPosition));
    }

    private void UpdateUnobservedCornerPosition((int X, int Y)? expectedPosition)
    {
        if (expectedPosition is { } expected)
        {
            UpdatePosition(expected.X, expected.Y);
        }
        else if (_desktopBounds is { } bounds)
        {
            UpdatePosition(bounds.X, bounds.Y);
        }
    }

    private (int X, int Y)? GetTrackedPosition() =>
        HasKnownPosition ? (CurrentX, CurrentY) : null;

    private bool IsPlausibleDesktopCorner((int X, int Y) position) =>
        _desktopBounds is { } bounds
        && IsWithinCornerTolerance(position, (bounds.X, bounds.Y));

    private static bool IsWithinCornerTolerance(
        (int X, int Y) position,
        (int X, int Y) expected)
    {
        return Math.Abs((long)position.X - expected.X) <= CornerPositionTolerance
            && Math.Abs((long)position.Y - expected.Y) <= CornerPositionTolerance;
    }

    private async Task<(int X, int Y)?> QueryPositionAsync(CancellationToken cancellationToken)
    {
        if (_positionProvider is null || !_positionProvider.HasUsableAbsolutePosition())
        {
            return null;
        }

        try
        {
            return await _positionProvider.GetAbsolutePositionAsync()
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[PlaybackCoordinator] Failed to synchronize cursor position");
            return null;
        }
    }

    private async Task<(int X, int Y)?> SynchronizeAfterRawMovementAsync(
        CancellationToken cancellationToken)
    {
        (int X, int Y)? lastObservedPosition = null;

        for (var attempt = 0; attempt < RawMovementPositionRefreshAttempts; attempt++)
        {
            var position = await QueryPositionAsync(cancellationToken).ConfigureAwait(false);
            if (position is not null)
            {
                lastObservedPosition = position;
                bool changedFromReference = _positionBeforeRawMovement is { } reference
                    && position.Value != reference;
                bool observedLongEnoughWithoutReference = _positionBeforeRawMovement is null
                    && attempt + 1 >= RawMovementMinimumRefreshAttemptsWithoutReference;
                if (changedFromReference || observedLongEnoughWithoutReference)
                {
                    return position;
                }
            }

            if (attempt + 1 < RawMovementPositionRefreshAttempts)
            {
                await Task.Delay(
                    RawMovementPositionRefreshInterval,
                    TimeProvider.System,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return lastObservedPosition;
    }
}
