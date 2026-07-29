
namespace CrossMacro.UI.Startup;

public sealed class GuiBootstrapContext(
    Action<IServiceCollection> configureServices,
    Action<IServiceCollection> configureRuntimeServices,
    GuiStartupOptions startupOptions)
{
    public Action<IServiceCollection> ConfigureServices { get; } = configureServices ?? throw new ArgumentNullException(nameof(configureServices));

    public Action<IServiceCollection> ConfigureRuntimeServices { get; } = configureRuntimeServices ?? throw new ArgumentNullException(nameof(configureRuntimeServices));

    public GuiStartupOptions StartupOptions { get; } = startupOptions ?? throw new ArgumentNullException(nameof(startupOptions));
}
