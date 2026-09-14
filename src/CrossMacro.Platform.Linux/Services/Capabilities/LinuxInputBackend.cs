namespace CrossMacro.Platform.Linux.Services.Capabilities;

/// <summary>The concrete input backend selected independently of diagnostic wording.</summary>
public enum LinuxInputBackend
{
    Unavailable,
    NativeX11,
    Daemon,
    DirectDevice,
}
