
namespace CrossMacro.Cli.Parsing;

/// <summary>
/// Option state shared by the schedule, shortcut and trigger parsers: the common
/// <c>--json</c>/<c>--log-level</c>/<c>--help</c> options plus <c>--name</c>, <c>--macro</c> and <c>--enabled</c>.
/// </summary>
internal abstract class TaskParseState
{
    public bool JsonOutput;
    public string? LogLevel;
    public string? TaskId;
    public string? Name;
    public string? MacroFilePath;
    public bool? Enabled;

    /// <summary>
    /// Handles the options every task command shares. Returns false when <paramref name="args"/>[<paramref name="index"/>]
    /// is not one of them; a non-null <paramref name="result"/> means parsing must stop with that result.
    /// </summary>
    public bool TryHandleSharedOption(string[] args, ref int index, string helpTopic, out CliParseResult? result)
    {
        if (CliParseHelpers.TryHandleCommonCliOption(args, ref index, helpTopic, ref JsonOutput, ref LogLevel, out result))
        {
            return true;
        }

        var token = args[index];
        if (string.Equals(token, "--name", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, JsonOutput, out Name);
            return true;
        }

        if (string.Equals(token, "--macro", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, JsonOutput, out MacroFilePath);
            return true;
        }

        if (string.Equals(token, "--enabled", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadBoolOption(args, ref index, JsonOutput, out Enabled);
            return true;
        }

        return false;
    }
}
