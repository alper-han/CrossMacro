
namespace CrossMacro.Infrastructure.Services;

public sealed class RuntimeContext(
    Func<string, string?> getEnvironmentVariable,
    Func<string, bool> fileExists) : IRuntimeContext, IDisplayEnvironmentDiagnostic
{
    private readonly Func<string, string?> _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
    private readonly Func<string, bool> _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));

    public RuntimeContext()
        : this(Environment.GetEnvironmentVariable, File.Exists)
    {
    }

    public bool IsLinux => OperatingSystem.IsLinux();
    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsMacOS => OperatingSystem.IsMacOS();

    public bool IsFlatpak =>
        !string.IsNullOrWhiteSpace(_getEnvironmentVariable("FLATPAK_ID")) ||
        string.Equals(_getEnvironmentVariable("CROSSMACRO_FLATPAK"), "1", StringComparison.Ordinal) ||
        (IsLinux && _fileExists("/.flatpak-info"));

    public string? SessionType => _getEnvironmentVariable("XDG_SESSION_TYPE");
    public string? XdgSessionType => SessionType;
    public string? Display => _getEnvironmentVariable("DISPLAY");
    public string? WaylandDisplay => _getEnvironmentVariable("WAYLAND_DISPLAY");
}
