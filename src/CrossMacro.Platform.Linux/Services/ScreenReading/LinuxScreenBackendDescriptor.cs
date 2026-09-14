namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed record LinuxScreenBackendDescriptor(
    LinuxScreenReaderBackend Backend,
    Func<LinuxScreenReaderBackendCapability> Probe,
    Func<LinuxScreenReaderBackendCapability, IScreenFrameProvider> CreateProvider,
    Func<CancellationToken, Task<LinuxScreenReaderBackendCapability>>? ProbeAsynchronously = null)
{
    /// <summary>
    /// Runs legacy synchronous probes away from the caller context. Backends that
    /// own an asynchronous transport can provide a cancellation-aware probe instead.
    /// </summary>
    public Task<LinuxScreenReaderBackendCapability> ProbeAsync(CancellationToken cancellationToken) =>
        ProbeAsynchronously?.Invoke(cancellationToken) ?? ProbeOnThreadPoolAsync(cancellationToken);

    private async Task<LinuxScreenReaderBackendCapability> ProbeOnThreadPoolAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var probeTask = Task.Run(Probe, CancellationToken.None);
        return await probeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
