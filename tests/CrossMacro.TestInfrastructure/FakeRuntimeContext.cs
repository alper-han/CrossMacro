using CrossMacro.Platform.Abstractions.Runtime;

namespace CrossMacro.TestInfrastructure;

/// <summary>A runtime context that reports exactly one host OS.</summary>
public sealed class FakeRuntimeContext : IRuntimeContext
{
    private FakeRuntimeContext(bool isLinux, bool isWindows, bool isMacOS, bool isFlatpak, string? sessionType)
    {
        IsLinux = isLinux;
        IsWindows = isWindows;
        IsMacOS = isMacOS;
        IsFlatpak = isFlatpak;
        SessionType = sessionType;
    }

    public bool IsLinux { get; }

    public bool IsWindows { get; }

    public bool IsMacOS { get; }

    public bool IsFlatpak { get; }

    public string? SessionType { get; }

    public static FakeRuntimeContext Linux(bool isFlatpak = false, string? sessionType = null) =>
        new(isLinux: true, isWindows: false, isMacOS: false, isFlatpak, sessionType);

    public static FakeRuntimeContext Windows() =>
        new(isLinux: false, isWindows: true, isMacOS: false, isFlatpak: false, sessionType: null);

    public static FakeRuntimeContext MacOS() =>
        new(isLinux: false, isWindows: false, isMacOS: true, isFlatpak: false, sessionType: null);
}
