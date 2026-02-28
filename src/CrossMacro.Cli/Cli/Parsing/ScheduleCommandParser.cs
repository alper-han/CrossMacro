namespace CrossMacro.Cli;

internal static class ScheduleCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        return TaskCommandParser.Parse(
            args,
            "schedule",
            (jsonOutput, logLevel) => new ScheduleListCliOptions(jsonOutput, logLevel),
            (taskId, jsonOutput, logLevel) => new ScheduleRunCliOptions(taskId, jsonOutput, logLevel));
    }
}
