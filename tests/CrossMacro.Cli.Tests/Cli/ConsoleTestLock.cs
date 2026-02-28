namespace CrossMacro.Cli.Tests;

internal static class ConsoleTestLock
{
    internal static readonly object Gate = new();
}
