
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastSupportProbe : IPortalScreenCastSupportProbe
{
    private const int MaxPortalConfigBytes = 64 * 1024;

    public static PortalScreenCastSupportProbe Instance { get; } = new();

    private readonly LinuxEnvironmentSnapshot _environment;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, string?> _readAllText;

    private PortalScreenCastSupportProbe()
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot(), File.Exists, TryReadAllText) { /* Empty */ }

    internal PortalScreenCastSupportProbe(
        LinuxEnvironmentSnapshot environment,
        Func<string, bool> fileExists,
        Func<string, string?> readAllText)
    {
        _environment = environment;
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _readAllText = readAllText ?? throw new ArgumentNullException(nameof(readAllText));
    }

    public PortalScreenCastSupportResult ProbeSupport()
    {
        var providerDiagnostic = PortalScreenCastProviderDiagnostic.Describe(_environment, _fileExists, _readAllText);
        if (string.IsNullOrWhiteSpace(Tmds.DBus.Protocol.DBusAddress.Session))
        {
            return PortalScreenCastSupportResult.Unsupported(
                "D-Bus session bus is unavailable; XDG Desktop Portal ScreenCast requires a session bus.",
                providerDiagnostic);
        }

        return PortalPipeWireFrameCaptureFactory.CanLoadPipeWire()
            ? PortalScreenCastSupportResult.Supported(providerDiagnostic)
            : PortalScreenCastSupportResult.Unsupported(
                "libpipewire-0.3 is unavailable; XDG Desktop Portal ScreenCast requires PipeWire.",
                providerDiagnostic);
    }

    internal static string? TryReadAllText(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists || fileInfo.Length > MaxPortalConfigBytes)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
            if (stream.Length > MaxPortalConfigBytes)
            {
                return null;
            }

            var bytes = new byte[MaxPortalConfigBytes + 1];
            var bytesRead = 0;
            while (bytesRead < bytes.Length)
            {
                var read = stream.Read(bytes.AsSpan(bytesRead));
                if (read is 0)
                {
                    break;
                }

                bytesRead += read;
            }

            return bytesRead <= MaxPortalConfigBytes
                ? System.Text.Encoding.UTF8.GetString(bytes, 0, bytesRead)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
