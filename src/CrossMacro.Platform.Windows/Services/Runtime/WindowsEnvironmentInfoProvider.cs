using CrossMacro.Platform.Abstractions.Runtime;

namespace CrossMacro.Platform.Windows.Services.Runtime;

internal sealed class WindowsEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.Windows;

    /// <summary>
    /// Windows always uses its own window decorations with close button.
    /// </summary>
    public bool WindowManagerHandlesCloseButton => false;
}
