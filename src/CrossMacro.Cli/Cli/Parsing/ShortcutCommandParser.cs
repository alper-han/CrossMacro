namespace CrossMacro.Cli;

internal static class ShortcutCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        return TaskCommandParser.Parse(
            args,
            "shortcut",
            (jsonOutput, logLevel) => new ShortcutListCliOptions(jsonOutput, logLevel),
            (taskId, jsonOutput, logLevel) => new ShortcutRunCliOptions(taskId, jsonOutput, logLevel));
    }
}
