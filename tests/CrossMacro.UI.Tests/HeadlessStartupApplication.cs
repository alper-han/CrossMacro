using Avalonia.Headless;

namespace CrossMacro.UI.Tests;

public sealed class HeadlessStartupApplication : Avalonia.Application
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<HeadlessStartupApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
