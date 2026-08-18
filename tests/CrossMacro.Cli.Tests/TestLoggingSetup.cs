
namespace CrossMacro.Cli.Tests;

internal static class TestLoggingSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
    }
}
