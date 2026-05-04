using Avalonia;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Startup;
using Serilog;
using System;
using System.Reflection;

namespace CrossMacro.UI;

public static class Program
{
    private const string SingleInstanceName = "CrossMacro.UI.SingleInstance";

    public static int RunGui(
        string[] args,
        IPlatformServiceRegistrar platformServiceRegistrar,
        Func<AppBuilder, AppBuilder> configureAppBuilder)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);
        ArgumentNullException.ThrowIfNull(configureAppBuilder);

        var startupParseResult = GuiStartupOptionsParser.Parse(args);

        var bootstrapContext = new GuiBootstrapContext(platformServiceRegistrar, startupParseResult.Options);
        Log.Information("Starting CrossMacro application");

        return configureAppBuilder(BuildAvaloniaApp(bootstrapContext))
            .StartWithClassicDesktopLifetime(startupParseResult.ForwardedArgs);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(GuiBootstrapContext? bootstrapContext = null)
    {
        GC.KeepAlive(typeof(SvgImageExtension).Assembly);

        var builder = bootstrapContext == null
            ? AppBuilder.Configure<App>()
            : AppBuilder.Configure(() => new App(bootstrapContext));

        return builder
            .WithInterFont()
            .UseHarfBuzz()
            .LogToTrace()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Avalonia.Fonts.Inter/Assets#Inter",
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("avares://Avalonia.Fonts.Inter/Assets#Inter") }
                ]
            });
    }

    public static IDisposable? TryAcquireRuntimeSingleInstanceGuard()
    {
        return SingleInstanceGuard.TryAcquire(SingleInstanceName);
    }

    public static string GetVersionString()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = assembly.GetName();
        var version = name.Version;
        if (version == null)
        {
            return name.Name ?? "CrossMacro";
        }

        return $"{name.Name} {version.Major}.{version.Minor}.{version.Build}";
    }
}
