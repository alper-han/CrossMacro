using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using CrossMacro.UI.Hosting;

namespace CrossMacro.UI.MacOS;

[SupportedOSPlatform("macos")]
internal static class Program
{
    // Discovered by the Avalonia visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => new MacOSDesktopPlatform().ConfigureAppBuilder(CrossMacro.UI.Program.BuildAvaloniaApp());

    [System.STAThread]
    public static Task<int> Main(string[] args) => GuiHostBootstrap.RunAsync(args, new MacOSDesktopPlatform());
}
