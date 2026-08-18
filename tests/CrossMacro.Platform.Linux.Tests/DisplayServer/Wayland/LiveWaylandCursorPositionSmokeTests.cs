using System.Globalization;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

[Collection("EnvironmentVariableSensitive")]
public sealed class LiveWaylandCursorPositionSmokeTests(ITestOutputHelper output)
{
    private static readonly TimeSpan _smokeTimeout = TimeSpan.FromSeconds(10);
    private readonly ITestOutputHelper _output = output;

    [WaylandLiveCursorFact]
    public async Task CursorPosition_WhenAvailable_StaysInsideLogicalDesktopBounds()
    {
        using var timeout = new CancellationTokenSource(_smokeTimeout, TimeProvider.System);
        var provider = WaylandCursorPositionProvider.CreateOrThrow(timeout.Token);

        await using (provider)
        {
            var bounds = await provider.GetDesktopBoundsAsync().WaitAsync(timeout.Token)
                ?? throw new InvalidOperationException("Wayland desktop bounds are unavailable.");
            var position = await WaitForPositionAsync(provider, timeout.Token)
                ?? throw new InvalidOperationException("Wayland cursor position is unavailable.");
            _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Cursor: ({position.X}, {position.Y}); desktop: ({bounds.X}, {bounds.Y}) {bounds.Width}x{bounds.Height}"));

            Assert.True(
                bounds.Contains(new ScreenPoint(position.X, position.Y)),
                string.Create(CultureInfo.InvariantCulture,
                    $"Cursor position ({position.X}, {position.Y}) must be inside logical desktop bounds {bounds}."));
        }
    }

    [CosmicLiveInputFact]
    public async Task CosmicOutputMappedAbsoluteSimulation_ReachesTargetAndRestoresOrigin()
    {
        using var timeout = new CancellationTokenSource(_smokeTimeout, TimeProvider.System);
        var provider = WaylandCursorPositionProvider.CreateOrThrow(timeout.Token);

        await using (provider)
        await using (var client = new IpcClient())
        {
            var bounds = await provider.GetDesktopBoundsAsync()
                ?? throw new InvalidOperationException("Wayland desktop bounds are unavailable.");
            var outputs = await ((IOutputTopologyProvider)provider)
                .GetOutputBoundsAsync(timeout.Token);
            var before = await WaitForPositionAsync(provider, timeout.Token)
                ?? throw new InvalidOperationException("Initial Wayland cursor position is unavailable.");
            var currentOutput = outputs.FirstOrDefault(output =>
                output.Contains(new ScreenPoint(before.X, before.Y)));
            if (currentOutput == default)
            {
                throw new InvalidOperationException("The cursor is outside the advertised Wayland outputs.");
            }

            var targetOutput = outputs.FirstOrDefault(output => output != currentOutput);
            if (targetOutput == default)
            {
                targetOutput = currentOutput;
            }

            var target = (
                X: targetOutput.X + (targetOutput.Width / 2),
                Y: targetOutput.Y + (targetOutput.Height / 2));
            using var simulator = new CosmicAbsoluteInputSimulator(
                new LinuxIpcInputSimulator(client),
                provider,
                provider);
            await simulator.InitializeAsync(bounds.Width, bounds.Height, timeout.Token);

            try
            {
                simulator.MoveAbsolute(target.X - bounds.X, target.Y - bounds.Y);
                var after = await WaitForTargetAsync(provider, target, timeout.Token);
                _output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"COSMIC mapped absolute: ({before.X}, {before.Y}) -> ({after.X}, {after.Y}); target ({target.X}, {target.Y})"));

                Assert.InRange(after.X, target.X - 2, target.X + 2);
                Assert.InRange(after.Y, target.Y - 2, target.Y + 2);
            }
            finally
            {
                simulator.MoveAbsolute(before.X - bounds.X, before.Y - bounds.Y);
                _ = await WaitForTargetAsync(provider, before, timeout.Token);
            }
        }
    }

    private static async Task<(int X, int Y)> WaitForTargetAsync(
        WaylandCursorPositionProvider provider,
        (int X, int Y) target,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var position = await provider.GetAbsolutePositionAsync().ConfigureAwait(false);
            if (position is { } current &&
                Math.Abs(current.X - target.X) <= 2 &&
                Math.Abs(current.Y - target.Y) <= 2)
            {
                return current;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(10),
                TimeProvider.System,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<(int X, int Y)?> WaitForPositionAsync(
        WaylandCursorPositionProvider provider,
        CancellationToken cancellationToken)
    {
        var positionSource = new TaskCompletionSource<(int X, int Y)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void OnPositionChanged(object? sender, MousePositionChangedEventArgs args) =>
            positionSource.TrySetResult((args.X, args.Y));

        provider.PositionChanged += OnPositionChanged;
        try
        {
            var position = await provider.GetAbsolutePositionAsync().ConfigureAwait(false);
            return position ?? await positionSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            provider.PositionChanged -= OnPositionChanged;
        }
    }
}
