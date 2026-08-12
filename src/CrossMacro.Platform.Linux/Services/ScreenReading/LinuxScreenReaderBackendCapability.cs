
namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public readonly record struct LinuxScreenReaderBackendCapability
{
    private LinuxScreenReaderBackendCapability(
        LinuxScreenReaderBackend backend,
        bool isAvailable,
        ScreenReadErrorKind? errorKind,
        string? errorMessage,
        string? details)
    {
        if (!isAvailable && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable screen reader backends require a message.", nameof(errorMessage));
        }

        Backend = backend;
        IsAvailable = isAvailable;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
        Details = details;
    }

    public LinuxScreenReaderBackend Backend { get; }

    public bool IsAvailable { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public string? Details { get; }

    public static LinuxScreenReaderBackendCapability Available(LinuxScreenReaderBackend backend, string? details = null) =>
        new(backend, isAvailable: true, errorKind: null, errorMessage: null, details);

    public static LinuxScreenReaderBackendCapability Unavailable(
        LinuxScreenReaderBackend backend,
        ScreenReadErrorKind errorKind,
        string errorMessage,
        string? details = null) =>
        new(backend, isAvailable: false, errorKind, errorMessage, details);
}
