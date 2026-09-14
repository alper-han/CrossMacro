namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Adapts the original detector-based construction API to the canonical snapshot
/// selection path. Production composition supplies its snapshot provider directly.
/// </summary>
internal sealed class LegacyLinuxInputSnapshotAdapter : ILinuxCapabilitySnapshotProvider
{
    private readonly ILinuxEnvironmentDetector _environment;
    private readonly ILinuxInputCapabilityDetector _input;

    internal LegacyLinuxInputSnapshotAdapter(
        ILinuxEnvironmentDetector environment,
        ILinuxInputCapabilityDetector input)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public LinuxCapabilitySnapshot GetSnapshot()
    {
        var compositor = _environment.DetectedCompositor;
        if (!_environment.IsWayland)
        {
            // The original factory tried native X11 for every non-Wayland session.
            compositor = CompositorType.X11;
        }
        else if (compositor is CompositorType.Unknown or CompositorType.X11)
        {
            compositor = CompositorType.Other;
        }

        var snapshot = new LinuxCapabilitySnapshot(default, compositor, default,
            LinuxScreenReaderCapabilitySnapshot.NotApplicable("Input-only compatibility snapshot."));
        return snapshot.IsX11 ? snapshot : CompleteInputSnapshot(snapshot);
    }

    internal LinuxCapabilitySnapshot CompleteInputSnapshot(LinuxCapabilitySnapshot snapshot)
    {
        var input = _input.GetSnapshot() with
        {
            ResolvedMode = _input.DetermineMode(),
            CanReadInputEvents = _input.CanReadInputEvents,
        };
        return snapshot with { Input = input };
    }

    public void InvalidateCache() => _input.InvalidateCache();

    public void InvalidateScreenReadingCache() { }
}
