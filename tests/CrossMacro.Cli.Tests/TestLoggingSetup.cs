
namespace CrossMacro.Cli.Tests;

internal static class TestLoggingSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        FluentAssertions.License.Accepted = true;
        LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
    }
}
