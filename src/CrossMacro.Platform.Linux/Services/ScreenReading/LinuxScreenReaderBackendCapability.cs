using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public readonly record struct LinuxScreenReaderBackendCapability
{
    private LinuxScreenReaderBackendCapability(
        LinuxScreenReaderBackend backend,
        bool isAvailable,
        ScreenReadErrorKind? errorKind,
        string? errorMessage)
    {
        if (!isAvailable && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable screen reader backends require a message.", nameof(errorMessage));
        }

        Backend = backend;
        IsAvailable = isAvailable;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public LinuxScreenReaderBackend Backend { get; }

    public bool IsAvailable { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static LinuxScreenReaderBackendCapability Available(LinuxScreenReaderBackend backend) =>
        new(backend, true, null, null);

    public static LinuxScreenReaderBackendCapability Unavailable(
        LinuxScreenReaderBackend backend,
        ScreenReadErrorKind errorKind,
        string errorMessage) =>
        new(backend, false, errorKind, errorMessage);
}
