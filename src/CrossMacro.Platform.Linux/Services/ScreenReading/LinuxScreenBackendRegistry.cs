namespace CrossMacro.Platform.Linux.Services.ScreenReading;

/// <summary>Backend identity, capability acquisition and lazy provider construction share one registration.</summary>
internal sealed class LinuxScreenBackendRegistry
{
    private readonly IReadOnlyDictionary<LinuxScreenReaderBackend, LinuxScreenBackendDescriptor> _descriptors;

    public LinuxScreenBackendRegistry(IEnumerable<LinuxScreenBackendDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _descriptors = descriptors.ToDictionary(descriptor => descriptor.Backend);
    }

    public IScreenFrameProvider CreateProvider(LinuxScreenReaderBackendCapability capability) => Get(capability.Backend).CreateProvider(capability);

    public LinuxScreenReaderCapabilitySnapshot ProbeSnapshot()
    {
        // Preserve the acquisition order independently of backend selection priority.
        var ext = Get(LinuxScreenReaderBackend.ExtImageCopy).Probe();
        var wlr = Get(LinuxScreenReaderBackend.WlrScreencopy).Probe();
        var portal = Get(LinuxScreenReaderBackend.Portal).Probe();
        var kwin = Get(LinuxScreenReaderBackend.KWinScreenShot2).Probe();
        var gnome = Get(LinuxScreenReaderBackend.GnomeExtension).Probe();
        return new LinuxScreenReaderCapabilitySnapshot(kwin, ext, wlr, portal, gnome);
    }

    /// <summary>
    /// Acquires backend capability information without running synchronous probe work
    /// on the caller's context. The order remains intentional because later
    /// diagnostics expose every backend in a stable order.
    /// </summary>
    public async Task<LinuxScreenReaderCapabilitySnapshot> ProbeSnapshotAsync(CancellationToken cancellationToken)
    {
        var ext = await ProbeAsync(LinuxScreenReaderBackend.ExtImageCopy, cancellationToken).ConfigureAwait(false);
        var wlr = await ProbeAsync(LinuxScreenReaderBackend.WlrScreencopy, cancellationToken).ConfigureAwait(false);
        var portal = await ProbeAsync(LinuxScreenReaderBackend.Portal, cancellationToken).ConfigureAwait(false);
        var kwin = await ProbeAsync(LinuxScreenReaderBackend.KWinScreenShot2, cancellationToken).ConfigureAwait(false);
        var gnome = await ProbeAsync(LinuxScreenReaderBackend.GnomeExtension, cancellationToken).ConfigureAwait(false);
        return new LinuxScreenReaderCapabilitySnapshot(kwin, ext, wlr, portal, gnome);
    }

    private LinuxScreenBackendDescriptor Get(LinuxScreenReaderBackend backend) =>
        _descriptors.TryGetValue(backend, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown Linux screen reader backend.");

    private async Task<LinuxScreenReaderBackendCapability> ProbeAsync(
        LinuxScreenReaderBackend backend,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Get(backend).ProbeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log.Warning(exception, "[LinuxScreenBackendRegistry] {Backend} capability probe failed", backend);
            return LinuxScreenReaderBackendCapability.Unavailable(
                backend,
                ScreenReadErrorKind.BackendUnavailable,
                exception.Message);
        }
    }
}
