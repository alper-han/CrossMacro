using System;

namespace CrossMacro.Infrastructure.Linux.Native;

/// <summary>
/// Centralized constants for Linux system paths.
/// </summary>
public static class LinuxSystemPaths
{
    /// <summary>
    /// Path to the group file (/etc/group).
    /// </summary>
    public const string GroupFile = "/etc/group";

    /// <summary>
    /// Path to the password file (/etc/passwd).
    /// </summary>
    public const string PasswdFile = "/etc/passwd";

    /// <summary>
    /// Default runtime directory for the daemon socket and logs.
    /// Typically managed by systemd.
    /// </summary>
    public const string RuntimeDirectory = "/run/crossmacro";

    /// <summary>
    /// Primary uinput device path.
    /// </summary>
    public const string UInputDevicePath = "/dev/uinput";

    /// <summary>
    /// Alternate uinput device path (some systems use this).
    /// </summary>
    public const string UInputAlternatePath = "/dev/input/uinput";
}
