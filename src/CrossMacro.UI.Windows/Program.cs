using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using CrossMacro.UI.Hosting;
using CrossMacro.UI.Windows.Native;

namespace CrossMacro.UI.Windows;

[SupportedOSPlatform("windows")]
internal static class Program
{
    // Discovered by the Avalonia visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => new WindowsDesktopPlatform().ConfigureAppBuilder(CrossMacro.UI.Program.BuildAvaloniaApp());

    [System.STAThread]
    public static Task<int> Main(string[] args)
    {
        WindowsNativeLibrariesBootstrapper.EnsureLoaded();
        return RunAppAsync(args);
    }

    // Kept out of line so that compiling Main does not resolve the Avalonia/Skia-dependent
    // types used here before EnsureLoaded has made the native libraries available.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Task<int> RunAppAsync(string[] args) =>
        GuiHostBootstrap.RunAsync(args, new WindowsDesktopPlatform());
}
