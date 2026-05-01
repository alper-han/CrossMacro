using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Extensions;
using CrossMacro.Core.Logging;

namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Factory responsible for creating the appropriate IMousePositionProvider
/// based on the detected Linux desktop environment.
/// Single Responsibility: Only handles position provider creation.
/// </summary>
public class LinuxPositionProviderFactory
{
    private readonly IEnumerable<IPositionProviderSelector> _selectors;
    private readonly ILinuxEnvironmentDetector _environmentDetector;

    public LinuxPositionProviderFactory(
        IEnumerable<IPositionProviderSelector> selectors,
        ILinuxEnvironmentDetector environmentDetector)
    {
        _selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
    }

    /// <summary>
    /// Creates the appropriate position provider for the current desktop environment.
    /// </summary>
    public IMousePositionProvider Create()
    {
        var compositorType = _environmentDetector.DetectedCompositor;
        
        LoggingExtensions.LogOnce("LinuxPositionProviderFactory_Compositor", "[LinuxPositionProviderFactory] Compositor: {Compositor}", compositorType);

        var provider = _selectors
            .Where(s => s.CanHandle(compositorType))
            .OrderByDescending(s => s.Priority)
            .FirstOrDefault()
            ?.Create();

        if (provider == null)
        {
           // Should ideally not happen if Fallback selector is registered, but as a safety net:
           Log.Warning("[LinuxPositionProviderFactory] No matching selector found for {Compositor}, using Fallback.", compositorType);
           return new FallbackPositionProvider();
        }

        return provider;
    }
}
