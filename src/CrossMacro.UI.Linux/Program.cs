
using System.Runtime.Versioning;
using CrossMacro.UI.Hosting;
using CrossMacro.UI.Services.Desktop;
namespace CrossMacro.UI.Linux;

[SupportedOSPlatform("linux")]
internal static class Program
{
    // Discovered by the Avalonia visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => CreatePlatform().ConfigureAppBuilder(CrossMacro.UI.Program.BuildAvaloniaApp());

    [System.STAThread]
    public static Task<int> Main(string[] args) => GuiHostBootstrap.RunAsync(args, CreatePlatform());

    private static LinuxDesktopPlatform CreatePlatform() =>
        new(LinuxEnvironmentVariables.CaptureCurrentSnapshot());
}
