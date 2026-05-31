using Xunit;
using CrossMacro.Infrastructure.Logging;
using System.Runtime.CompilerServices;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CrossMacro.Cli.Tests;

internal static class TestLoggingSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        LoggerSetup.Initialize("Fatal", enableFileLogging: false, enableConsoleLogging: false);
    }
}
