using System;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.UI.Startup;

public sealed class GuiBootstrapContext
{
    public GuiBootstrapContext(IPlatformServiceRegistrar platformServiceRegistrar, GuiStartupOptions startupOptions)
    {
        PlatformServiceRegistrar = platformServiceRegistrar ?? throw new ArgumentNullException(nameof(platformServiceRegistrar));
        StartupOptions = startupOptions ?? throw new ArgumentNullException(nameof(startupOptions));
    }

    public IPlatformServiceRegistrar PlatformServiceRegistrar { get; }

    public GuiStartupOptions StartupOptions { get; }
}
