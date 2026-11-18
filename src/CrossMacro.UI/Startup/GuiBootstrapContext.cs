
namespace CrossMacro.UI.Startup;

public sealed class GuiBootstrapContext(
    Action<IServiceCollection> configureServices,
    GuiStartupOptions startupOptions)
{
    public Action<IServiceCollection> ConfigureServices { get; } = configureServices ?? throw new ArgumentNullException(nameof(configureServices));

    public GuiStartupOptions StartupOptions { get; } = startupOptions ?? throw new ArgumentNullException(nameof(startupOptions));
}
