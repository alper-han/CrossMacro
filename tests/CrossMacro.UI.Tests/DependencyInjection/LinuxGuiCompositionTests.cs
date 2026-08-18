
using System.Runtime.Versioning;

namespace CrossMacro.UI.Tests.DependencyInjection;

[SupportedOSPlatform("linux")]
public sealed class LinuxGuiCompositionTests
{
    [Fact]
    public async Task FullLinuxGuiComposition_ResolvesStartupServicesWithoutClipboardCycle()
    {
        var services = new ServiceCollection();
        var environment = new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
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
        CrossMacro.UI.Hosting.GuiHostBootstrap.ConfigureGuiRuntimeServices(services);
        _ = services.AddCrossMacroServices();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var startupCoordinator = provider.GetRequiredService<IDesktopStartupCoordinator>();
        var textExpansionService = provider.GetRequiredService<ITextExpansionService>();
        var clipboard = provider.GetRequiredService<IClipboardService>();
        var imageClipboard = provider.GetRequiredService<IImageClipboardService>();
        var profileManager = provider.GetRequiredService<IProfileManager>();
        var triggerService = provider.GetRequiredService<ITriggerService>();

        var nativeClipboard = provider.GetRequiredService<LinuxNativeClipboardService>();

        Assert.NotNull(startupCoordinator);
        Assert.NotNull(textExpansionService);
        Assert.Same(nativeClipboard, clipboard);
        Assert.Same(nativeClipboard, imageClipboard);
        Assert.Same(nativeClipboard, provider.GetRequiredService<ILinuxClipboardService>());
        TestAssertions.IsType<ProfileRuntimeCoordinator>(profileManager);
        TestAssertions.IsType<TriggerService>(triggerService);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(Func<IProfileManager>));
    }
}
