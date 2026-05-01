using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Windows.Services;

/// <summary>
/// Windows implementation of IEnvironmentInfoProvider.
/// </summary>
public class WindowsEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.Windows;
    
    /// <summary>
    /// Windows always uses its own window decorations with close button.
    /// </summary>
    public bool WindowManagerHandlesCloseButton => false;
}
