
namespace CrossMacro.UI.Startup;

public sealed class GuiBootstrapContext
{
    public GuiBootstrapContext(
        Action<IServiceCollection> configureServices,
        Action<IServiceCollection> configureRuntimeServices,
        GuiStartupOptions startupOptions)
    {
        ConfigureServices = configureServices ?? throw new ArgumentNullException(nameof(configureServices));
        ConfigureRuntimeServices = configureRuntimeServices ?? throw new ArgumentNullException(nameof(configureRuntimeServices));
        StartupOptions = startupOptions ?? throw new ArgumentNullException(nameof(startupOptions));
    }

    public Action<IServiceCollection> ConfigureServices { get; }

    public Action<IServiceCollection> ConfigureRuntimeServices { get; }

    public GuiStartupOptions StartupOptions { get; }
}
