using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.MacOS.Services;

/// <summary>
/// macOS implementation of IEnvironmentInfoProvider.
/// </summary>
public class MacOSEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.MacOS;
    
    /// <summary>
    /// macOS always uses its own window decorations with traffic light buttons.
    /// </summary>
    public bool WindowManagerHandlesCloseButton => false;
}
