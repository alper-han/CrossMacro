using System.Runtime.Versioning;
using CrossMacro.Cli;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Core.Services.Clipboard;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Infrastructure.Services.Automation.Triggers;
using CrossMacro.Infrastructure.Services.Profiles;
using CrossMacro.Platform.Linux.Clipboard;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Runtime;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Hosting;
using CrossMacro.UI.Linux;
using CrossMacro.UI.Linux.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Tests.DependencyInjection;

[SupportedOSPlatform("linux")]
public sealed class LinuxGuiCompositionTests
{
    private static readonly LinuxEnvironmentSnapshot WaylandEnvironment = new(
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

    [LinuxFact]
    public async Task FullLinuxGuiComposition_ResolvesStartupServicesWithoutClipboardCycle()
    {
        var services = new ServiceCollection();

        _ = services.AddSingleton(GuiStartupOptions.Default);
        _ = services.AddCrossMacroGuiHostServices(new LinuxDesktopPlatform(WaylandEnvironment));
        _ = services.AddCrossMacroServices();

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var startupCoordinator = provider.GetRequiredService<DesktopStartupCoordinator>();
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
        _ = Assert.IsType<ProfileRuntimeCoordinator>(profileManager);
        _ = Assert.IsType<TriggerService>(triggerService);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(Func<IProfileManager>));
    }

    [LinuxFact]
    public void LinuxGuiComposition_UsesTheStatusNotifierTrayBackend()
    {
        var services = new ServiceCollection();

        _ = services.AddSingleton(GuiStartupOptions.Default);
        _ = services.AddCrossMacroGuiHostServices(new LinuxDesktopPlatform(WaylandEnvironment));
        _ = services.AddCrossMacroServices();

        var tray = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ITrayIconBackend));
        Assert.Equal(typeof(LinuxStatusNotifierTrayBackend), tray.ImplementationType);
    }

    [LinuxFact]
    public async Task LinuxCliComposition_ResolvesEveryRegistrationForEachRuntimeProfile()
    {
        foreach (var profile in new[] { CliRuntimeProfile.OneShot, CliRuntimeProfile.Persistent })
        {
            var services = new ServiceCollection();

            _ = services.AddCrossMacroCliRuntimeServices(new LinuxDesktopPlatform(WaylandEnvironment), profile);
            _ = services.AddCliServices();

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            var nativeClipboard = provider.GetRequiredService<LinuxNativeClipboardService>();
            Assert.Same(nativeClipboard, provider.GetRequiredService<IClipboardService>());
            Assert.Same(nativeClipboard, provider.GetRequiredService<IImageClipboardReader>());
            Assert.IsType<LinuxRuntimeContext>(provider.GetRequiredService<IRuntimeContext>());
            Assert.NotNull(provider.GetRequiredService<CliCommandExecutor>());
            Assert.Null(provider.GetService<ITrayIconBackend>());
        }
    }
}
