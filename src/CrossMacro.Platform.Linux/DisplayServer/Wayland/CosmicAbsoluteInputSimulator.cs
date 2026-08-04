namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Adapts desktop-wide absolute coordinates to COSMIC's active-output mapping.
/// COSMIC maps an absolute uinput device over the output that currently owns the pointer,
/// so cross-output moves first traverse connected output edges and then send output-local
/// absolute coordinates through the wrapped backend.
/// </summary>
internal sealed class CosmicAbsoluteInputSimulator(
    IInputSimulator inner,
    IMousePositionProvider positionProvider,
    IOutputTopologyProvider outputTopologyProvider) :
    IInputSimulator,
    IInputSimulatorCapabilities,
    IInputSimulatorAbsoluteBounds,
    IBatchedInputSimulator,
    IAsyncBatchedInputSimulator
{
    private const int OutputCrossingDelta = 8;

    private readonly IInputSimulator _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IMousePositionProvider _positionProvider = positionProvider
        ?? throw new ArgumentNullException(nameof(positionProvider));
    private readonly IOutputTopologyProvider _outputTopologyProvider = outputTopologyProvider
        ?? throw new ArgumentNullException(nameof(outputTopologyProvider));

    private IReadOnlyList<ScreenRect> _outputs = [];
    private ScreenRect? _desktopBounds;
    private int _screenWidth;
    private int _screenHeight;
    private int _activeOutputIndex = -1;
    private bool _absoluteMappingReady;
    private bool _disposed;

    public string ProviderName => $"{_inner.ProviderName} (COSMIC output-mapped absolute)";
    public bool IsSupported => !_disposed && _inner.IsSupported;
    public bool SupportsAbsoluteCoordinates =>
        !_disposed &&
        _absoluteMappingReady &&
        _inner is IInputSimulatorCapabilities { SupportsAbsoluteCoordinates: true };
    public bool UsesZeroBasedScreenBounds => true;
    public bool SupportsBatchedInput =>
        !_disposed &&
        _inner is IBatchedInputSimulator { SupportsBatchedInput: true };

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        InitializeAsync(screenWidth, screenHeight).GetAwaiter().GetResult();
    }

    public async Task InitializeAsync(
        int screenWidth = 0,
        int screenHeight = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _inner
            .InitializeAsync(screenWidth, screenHeight, cancellationToken)
            .ConfigureAwait(false);

        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
        _activeOutputIndex = -1;
        _absoluteMappingReady = false;
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return;
        }

        await RefreshTopologyAsync(cancellationToken).ConfigureAwait(false);
        _absoluteMappingReady =
            _outputs.Count > 0 &&
            _desktopBounds is not null &&
            (_outputs.Count is 1 || _positionProvider.HasUsableAbsolutePosition());
        if (_absoluteMappingReady)
        {
            Log.Debug(
                "[CosmicAbsoluteInputSimulator] Desktop-wide absolute input mapped across {OutputCount} COSMIC output(s)",
                _outputs.Count);
        }
        else
        {
            Log.Warning(
                "[CosmicAbsoluteInputSimulator] COSMIC output topology is unavailable; absolute input is disabled");
        }
    }

    public void MoveAbsolute(int x, int y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!SupportsAbsoluteCoordinates || _desktopBounds is not { } desktopBounds)
        {
            throw new InvalidOperationException(
                "COSMIC desktop-wide absolute input requires a usable output topology.");
        }

        int globalX = checked(desktopBounds.X + Math.Clamp(x, 0, _screenWidth - 1));
        int globalY = checked(desktopBounds.Y + Math.Clamp(y, 0, _screenHeight - 1));
        var target = ClampToNearestOutput(globalX, globalY);
        int targetOutputIndex = FindTargetOutput(target.X, target.Y);

        EnsureActiveOutput();
        if (_activeOutputIndex != targetOutputIndex)
        {
            var path = FindOutputPath(_activeOutputIndex, targetOutputIndex)
                ?? throw new InvalidOperationException(
                    "COSMIC cannot route the pointer between disconnected display outputs.");

            for (int index = 1; index < path.Count; index++)
            {
                CrossOutput(path[index - 1], path[index]);
            }
        }

        SendOutputLocalAbsolute(targetOutputIndex, target.X, target.Y);
        _activeOutputIndex = targetOutputIndex;
    }

    public void MoveRelative(int dx, int dy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _inner.MoveRelative(dx, dy);
        _activeOutputIndex = -1;
    }

    public void MouseButton(int button, bool pressed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _inner.MouseButton(button, pressed);
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _inner.Scroll(delta, isHorizontal);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _inner.KeyPress(keyCode, pressed);
    }

    public void Sync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _inner.Sync();
    }

    public void SimulateBatch(ReadOnlySpan<InputSimulationStep> steps)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_inner is not IBatchedInputSimulator { SupportsBatchedInput: true } batched)
        {
            throw new NotSupportedException("The wrapped input simulator does not support batched input.");
        }

        batched.SimulateBatch(steps);
    }

    public async Task SimulateBatchAsync(
        IReadOnlyList<InputSimulationStep> steps,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_inner is IAsyncBatchedInputSimulator asyncBatched)
        {
            await asyncBatched.SimulateBatchAsync(steps, cancellationToken).ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_inner is IBatchedInputSimulator { SupportsBatchedInput: true } batched)
        {
            batched.SimulateBatch(steps.ToArray());
            return;
        }

        throw new NotSupportedException("The wrapped input simulator does not support batched input.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inner.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RefreshTopologyAsync(CancellationToken cancellationToken)
    {
        var outputs = await _outputTopologyProvider
            .GetOutputBoundsAsync(cancellationToken)
            .ConfigureAwait(false);
        _outputs = outputs
            .Where(static output => output.Width > 0 && output.Height > 0)
            .Distinct()
            .ToArray();
        _desktopBounds = _outputs.Count > 0 ? ComputeDesktopBounds(_outputs) : null;
    }

    private void EnsureActiveOutput()
    {
        if (_activeOutputIndex >= 0 && _activeOutputIndex < _outputs.Count)
        {
            return;
        }

        var position = _positionProvider.GetAbsolutePositionAsync().GetAwaiter().GetResult();
        if (position is not null)
        {
            _activeOutputIndex = FindContainingOutput(position.Value.X, position.Value.Y);
        }

        if (_activeOutputIndex < 0 && _outputs.Count is 1)
        {
            _activeOutputIndex = 0;
        }

        if (_activeOutputIndex < 0)
        {
            throw new InvalidOperationException(
                "COSMIC's active output cannot be determined because the cursor position is unavailable.");
        }
    }

    private void CrossOutput(int sourceIndex, int destinationIndex)
    {
        var source = _outputs[sourceIndex];
        var destination = _outputs[destinationIndex];
        var transition = GetTransition(source, destination)
            ?? throw new InvalidOperationException(
                "COSMIC output routing encountered non-adjacent displays.");

        SendOutputLocalAbsolute(sourceIndex, transition.X, transition.Y);
        _inner.MoveRelative(
            transition.DeltaX * OutputCrossingDelta,
            transition.DeltaY * OutputCrossingDelta);
        _activeOutputIndex = destinationIndex;
    }

    private void SendOutputLocalAbsolute(int outputIndex, int globalX, int globalY)
    {
        var output = _outputs[outputIndex];
        int localX = Math.Clamp(globalX - output.X, 0, output.Width - 1);
        int localY = Math.Clamp(globalY - output.Y, 0, output.Height - 1);
        int deviceX = ScaleToDeviceAxis(localX, output.Width, _screenWidth);
        int deviceY = ScaleToDeviceAxis(localY, output.Height, _screenHeight);
        _inner.MoveAbsolute(deviceX, deviceY);
    }

    private int FindTargetOutput(int x, int y)
    {
        if (_activeOutputIndex >= 0 &&
            _activeOutputIndex < _outputs.Count &&
            _outputs[_activeOutputIndex].Contains(new ScreenPoint(x, y)))
        {
            return _activeOutputIndex;
        }

        int index = FindContainingOutput(x, y);
        if (index < 0)
        {
            throw new InvalidOperationException("The absolute pointer target is outside all COSMIC outputs.");
        }

        return index;
    }

    private int FindContainingOutput(int x, int y)
    {
        var point = new ScreenPoint(x, y);
        for (int index = 0; index < _outputs.Count; index++)
        {
            if (_outputs[index].Contains(point))
            {
                return index;
            }
        }

        return -1;
    }

    private (int X, int Y) ClampToNearestOutput(int x, int y)
    {
        if (FindContainingOutput(x, y) >= 0)
        {
            return (x, y);
        }

        double shortestDistance = double.MaxValue;
        var nearest = (X: x, Y: y);
        foreach (var output in _outputs)
        {
            int candidateX = Math.Clamp(x, output.X, output.Right - 1);
            int candidateY = Math.Clamp(y, output.Y, output.Bottom - 1);
            double deltaX = (double)x - candidateX;
            double deltaY = (double)y - candidateY;
            double distance = (deltaX * deltaX) + (deltaY * deltaY);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                nearest = (candidateX, candidateY);
            }
        }

        return nearest;
    }

    private IReadOnlyList<int>? FindOutputPath(int sourceIndex, int destinationIndex)
    {
        var previous = Enumerable.Repeat(-1, _outputs.Count).ToArray();
        var visited = new bool[_outputs.Count];
        var queue = new Queue<int>();
        visited[sourceIndex] = true;
        queue.Enqueue(sourceIndex);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (current == destinationIndex)
            {
                break;
            }

            for (int candidate = 0; candidate < _outputs.Count; candidate++)
            {
                if (visited[candidate] || GetTransition(_outputs[current], _outputs[candidate]) is null)
                {
                    continue;
                }

                visited[candidate] = true;
                previous[candidate] = current;
                queue.Enqueue(candidate);
            }
        }

        if (!visited[destinationIndex])
        {
            return null;
        }

        var path = new List<int>();
        for (int current = destinationIndex; current >= 0; current = previous[current])
        {
            path.Add(current);
            if (current == sourceIndex)
            {
                break;
            }
        }

        path.Reverse();
        return path;
    }

    private static OutputTransition? GetTransition(ScreenRect source, ScreenRect destination)
    {
        int overlapStartY = Math.Max(source.Y, destination.Y);
        int overlapEndY = Math.Min(source.Bottom, destination.Bottom);
        if (overlapEndY > overlapStartY)
        {
            int y = overlapStartY + ((overlapEndY - overlapStartY - 1) / 2);
            if (source.Right == destination.X)
            {
                return new OutputTransition(source.Right - 1, y, 1, 0);
            }

            if (destination.Right == source.X)
            {
                return new OutputTransition(source.X, y, -1, 0);
            }
        }

        int overlapStartX = Math.Max(source.X, destination.X);
        int overlapEndX = Math.Min(source.Right, destination.Right);
        if (overlapEndX > overlapStartX)
        {
            int x = overlapStartX + ((overlapEndX - overlapStartX - 1) / 2);
            if (source.Bottom == destination.Y)
            {
                return new OutputTransition(x, source.Bottom - 1, 0, 1);
            }

            if (destination.Bottom == source.Y)
            {
                return new OutputTransition(x, source.Y, 0, -1);
            }
        }

        return null;
    }

    private static int ScaleToDeviceAxis(int localCoordinate, int outputLength, int deviceLength)
    {
        long numerator = checked((long)localCoordinate * deviceLength);
        long rounded = (numerator + (outputLength / 2L)) / outputLength;
        return (int)Math.Clamp(rounded, 0, deviceLength - 1L);
    }

    private static ScreenRect ComputeDesktopBounds(IReadOnlyList<ScreenRect> outputs)
    {
        int minX = outputs.Min(static output => output.X);
        int minY = outputs.Min(static output => output.Y);
        int maxX = outputs.Max(static output => output.Right);
        int maxY = outputs.Max(static output => output.Bottom);
        return new ScreenRect(minX, minY, checked(maxX - minX), checked(maxY - minY));
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct OutputTransition(
        int X,
        int Y,
        int DeltaX,
        int DeltaY);
}
