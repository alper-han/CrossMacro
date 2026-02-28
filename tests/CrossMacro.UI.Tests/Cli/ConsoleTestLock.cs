namespace CrossMacro.UI.Tests.Cli;

internal static class ConsoleTestLock
{
    internal static readonly object Gate = new();
}
