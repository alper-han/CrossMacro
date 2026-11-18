
namespace CrossMacro.Commands.Tests;

internal static class TestLoggingSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
    }
}
