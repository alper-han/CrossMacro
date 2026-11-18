using System.Reflection;

namespace CrossMacro.UI;

public static class Program
{
    private const string SingleInstanceName = "CrossMacro.UI.SingleInstance";

    public static int RunGui(
        ReadOnlySpan<string> args,
        Action<IServiceCollection> configureServices,
        Func<AppBuilder, AppBuilder> configureAppBuilder)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        ArgumentNullException.ThrowIfNull(configureAppBuilder);

        var startupParseResult = GuiStartupOptionsParser.Parse(args);

        var bootstrapContext = new GuiBootstrapContext(configureServices, startupParseResult.Options);
        SerilogLog.Information("Starting CrossMacro application");

        return configureAppBuilder(BuildAvaloniaApp(bootstrapContext))
            .StartWithClassicDesktopLifetime(startupParseResult.ForwardedArgs.ToArray());
    }

    // Used by the host projects' BuildAvaloniaApp, which the Avalonia visual designer discovers.
    public static AppBuilder BuildAvaloniaApp(GuiBootstrapContext? bootstrapContext = null)
    {
        var builder = bootstrapContext is null
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
                    new FontFallback { FontFamily = new FontFamily("avares://Avalonia.Fonts.Inter/Assets#Inter") },
                ],
            });
    }

    public static IDisposable? TryAcquireRuntimeSingleInstanceGuard()
    {
        return SingleInstanceGuard.TryAcquire(SingleInstanceName);
    }

    public static bool TryActivateExistingRuntime() =>
        SingleInstanceActivationListener.TrySignal(SingleInstanceName);

    internal static SingleInstanceActivationListener? StartRuntimeActivationListener(Action activate) =>
        SingleInstanceActivationListener.TryStart(SingleInstanceName, activate);

    public static string GetVersionString()
    {
        var displayVersion = GetDisplayVersionString();
        var name = GetProductAssembly().GetName().Name ?? "CrossMacro";
        return displayVersion.Length is 0 ? name : $"{name} {displayVersion}";
    }

    public static string GetDisplayVersionString()
    {
        var assembly = GetProductAssembly();
        var version = assembly.GetName().Version;
        return version is null ? string.Empty : GetDisplayVersionString(assembly, version);
    }

    private static Assembly GetProductAssembly() => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    private static string GetDisplayVersionString(Assembly assembly, Version version)
    {
        var versionText = $"v{version.Major.ToString(CultureInfo.InvariantCulture)}.{version.Minor.ToString(CultureInfo.InvariantCulture)}.{version.Build.ToString(CultureInfo.InvariantCulture)}";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var revisionSeparator = informationalVersion?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        if (revisionSeparator < 0 || informationalVersion!.Length <= revisionSeparator + 1)
        {
            return versionText;
        }

        var revision = informationalVersion[(revisionSeparator + 1)..];
        return $"{versionText} ({revision[..Math.Min(revision.Length, 7)]})";
    }
}
