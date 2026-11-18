namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal sealed record LinuxScreenBackendDescriptor(
    LinuxScreenReaderBackend Backend,
    Func<CancellationToken, Task<LinuxScreenReaderBackendCapability>> ProbeAsync,
    Func<LinuxScreenReaderBackendCapability, IScreenFrameProvider> CreateProvider);
