using CrossMacro.Platform.Abstractions.Runtime;

namespace CrossMacro.Platform.MacOS.Services.Runtime;

/// <summary>
/// macOS implementation of IEnvironmentInfoProvider.
/// </summary>
internal sealed class MacOSEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.MacOS;

    /// <summary>
    /// macOS always uses its own window decorations with traffic light buttons.
    /// </summary>
    public bool WindowManagerHandlesCloseButton => false;
}
