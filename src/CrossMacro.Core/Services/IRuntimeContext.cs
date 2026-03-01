using System;
using System.IO;

namespace CrossMacro.Core.Services;

public interface IRuntimeContext
{
    bool IsLinux { get; }
    bool IsWindows { get; }
    bool IsMacOS { get; }
    bool IsFlatpak { get; }
    string? SessionType { get; }
}

public sealed class RuntimeContext : IRuntimeContext
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;

    public RuntimeContext()
        : this(Environment.GetEnvironmentVariable, File.Exists)
    {
    }

    public RuntimeContext(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
    }

    public bool IsLinux => OperatingSystem.IsLinux();
    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsMacOS => OperatingSystem.IsMacOS();

    public bool IsFlatpak =>
        !string.IsNullOrWhiteSpace(_getEnvironmentVariable("FLATPAK_ID")) ||
        string.Equals(_getEnvironmentVariable("CROSSMACRO_FLATPAK"), "1", StringComparison.Ordinal) ||
        (IsLinux && _fileExists("/.flatpak-info"));

    public string? SessionType => _getEnvironmentVariable("XDG_SESSION_TYPE");
}
