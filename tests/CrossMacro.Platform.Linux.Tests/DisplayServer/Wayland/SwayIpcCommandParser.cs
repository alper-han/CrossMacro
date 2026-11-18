using System.Text;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

/// <summary>
/// Models how Sway's IPC command execution (<c>execute_command</c> in sway/commands.c) tokenizes a command string:
/// commands are split on <c>;</c> and <c>,</c> outside quotes, arguments on whitespace outside quotes, and every
/// quote character is stripped from arguments that start with a quote. Backslashes are not modelled because the
/// production quoting rejects them.
/// </summary>
internal static class SwayIpcCommandParser
{
    private const string Whitespace = " \f\n\r\t\v";

    public static IReadOnlyList<SwayParsedCommand> Parse(string text)
    {
        var commands = new List<SwayParsedCommand>();
        var head = text.TrimStart();
        var criteria = string.Empty;
        if (head.StartsWith('[') && head.IndexOf(']', StringComparison.Ordinal) is var close and >= 0)
        {
            criteria = head[..(close + 1)];
            head = head[(close + 1)..].TrimStart();
        }

        foreach (var segment in SplitCommands(head))
        {
            var words = SplitArguments(segment.Trim());
            for (var i = 1; i < words.Count; i++)
            {
                if (words[i].Length > 0 && words[i][0] is '"' or '\'')
                {
                    words[i] = StripQuotes(words[i]);
                }
            }

            if (words.Count > 0)
            {
                commands.Add(new SwayParsedCommand(criteria, words));
            }
        }

        return commands;
    }

    private static List<string> SplitCommands(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        bool inString = false, inChar = false;
        foreach (var c in text)
        {
            if (c is '"' && !inChar)
            {
                inString = !inString;
            }
            else if (c is '\'' && !inString)
            {
                inChar = !inChar;
            }
            else if (c is ';' or ',' && !inString && !inChar)
            {
                parts.Add(current.ToString());
                _ = current.Clear();
                continue;
            }

            _ = current.Append(c);
        }

        parts.Add(current.ToString());
        return parts;
    }

    private static List<string> SplitArguments(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        bool inString = false, inChar = false;
        foreach (var c in text)
        {
            if (c is '"' && !inChar)
            {
                inString = !inString;
            }
            else if (c is '\'' && !inString)
            {
                inChar = !inChar;
            }
            else if (Whitespace.Contains(c, StringComparison.Ordinal) && !inString && !inChar)
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    _ = current.Clear();
                }

                continue;
            }

            _ = current.Append(c);
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }

    private static string StripQuotes(string text)
    {
        var result = new StringBuilder();
        bool inString = false, inChar = false;
        foreach (var c in text)
        {
            if (c is '\'' && !inString)
            {
                inChar = !inChar;
                continue;
            }

            if (c is '"' && !inChar)
            {
                inString = !inString;
                continue;
            }

            _ = result.Append(c);
        }

        return result.ToString();
    }
}
