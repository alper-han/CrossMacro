using Serilog;
using CrossMacro.Core.Wayland;

namespace CrossMacro.Infrastructure.Wayland
{
    /// <summary>
    /// Factory for creating the appropriate mouse position provider based on the detected compositor
    /// </summary>
    public static class MousePositionProviderFactory
    {
        /// <summary>
        /// Create a position provider based on the current Wayland compositor
        /// </summary>
        public static IMousePositionProvider CreateProvider()
        {
            var compositorType = CompositorDetector.DetectCompositor();
            
            Log.Information("[MousePositionProviderFactory] Detected compositor: {CompositorType}", compositorType);
            
            IMousePositionProvider provider = compositorType switch
            {
                CompositorType.HYPRLAND => new HyprlandPositionProvider(),
                CompositorType.KDE => new KdePositionProvider(),
                CompositorType.GNOME => new GnomePositionProvider(),
                CompositorType.Other => new FallbackPositionProvider(),
                CompositorType.Unknown => new FallbackPositionProvider(),
                _ => new FallbackPositionProvider()
            };
            
            if (provider.IsSupported)
            {
                Log.Information("[MousePositionProviderFactory] Using provider: {ProviderName}", provider.ProviderName);
            }
            else
            {
                Log.Warning("[MousePositionProviderFactory] Provider not supported, falling back to relative coordinates");
            }
            
            return provider;
        }
    }
}
