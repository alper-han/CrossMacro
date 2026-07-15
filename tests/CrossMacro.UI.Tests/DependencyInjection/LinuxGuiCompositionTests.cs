using System.Reflection;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using LinuxProgram = CrossMacro.UI.Linux.Program;

namespace CrossMacro.UI.Tests.DependencyInjection;

public class LinuxGuiCompositionTests
{
    [Fact]
    public async Task FullLinuxGuiComposition_ResolvesStartupServicesWithoutClipboardCycle()
    {
        var services = new ServiceCollection();
        var environment = new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
            UseDaemon: null,
            SessionType: "wayland",
            WaylandDisplay: "wayland-test",
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

        LinuxProgram.ConfigureGuiServices(services, environment);
        LinuxProgram.ConfigureGuiRuntimeServices(services);
        services.AddCrossMacroServices();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var startupCoordinator = provider.GetRequiredService<IDesktopStartupCoordinator>();
        var textExpansionService = provider.GetRequiredService<ITextExpansionService>();
        var clipboard = provider.GetRequiredService<IClipboardService>();

        var composite = Assert.IsType<CompositeClipboardService>(clipboard);
        var fallbackField = typeof(CompositeClipboardService).GetField(
            "_avaloniaService",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(startupCoordinator);
        Assert.NotNull(textExpansionService);
        Assert.NotNull(fallbackField);
        Assert.Same(provider.GetRequiredService<AvaloniaClipboardService>(), fallbackField!.GetValue(composite));
    }
}
