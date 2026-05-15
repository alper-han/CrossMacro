using System;
using System.IO;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class GsrCompatibilityService : IPlatformStartupNotificationProvider
{
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, string> _readAllText;

    public GsrCompatibilityService()
        : this(File.Exists, File.ReadAllText)
    {
    }

    internal GsrCompatibilityService(Func<string, bool> fileExists, Func<string, string> readAllText)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _readAllText = readAllText ?? throw new ArgumentNullException(nameof(readAllText));
    }

    public PlatformStartupNotification? GetStartupNotification()
    {
        return IsGsrVirtualKeyboardActive()
            ? new PlatformStartupNotification(
                "GPU Screen Recorder",
                "GSR is active. CrossMacro can read GSR's virtual keyboard, but GSR-owned hotkeys may still be swallowed by GSR.",
                PlatformStartupNotificationSeverity.Warning)
            : null;
    }

    internal bool IsGsrVirtualKeyboardActive()
    {
        try
        {
            if (!_fileExists(LinuxGsrCompatibility.InputDevicesPath))
            {
                return false;
            }

            var inputDevices = _readAllText(LinuxGsrCompatibility.InputDevicesPath);
            return LinuxGsrCompatibility.ContainsGsrVirtualKeyboard(inputDevices);
        }
        catch
        {
            return false;
        }
    }
}
