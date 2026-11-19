using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Builds Sway IPC command fragments from caller-supplied data so the data can never alter command structure.
/// </summary>
internal static class SwayCommandText
{
    /// <summary>
    /// Parses a window address as a Sway container id; anything but plain decimal digits is rejected.
    /// </summary>
    public static bool TryParseContainerId(string address, out long id) =>
        long.TryParse(address, NumberStyles.None, CultureInfo.InvariantCulture, out id);

    public static string ContainerCriteria(long containerId) =>
        $"[con_id={containerId.ToString(CultureInfo.InvariantCulture)}]";

    /// <summary>
    /// Quotes <paramref name="value"/> so Sway's IPC command parser reads it back as exactly one literal argument.
    /// </summary>
    /// <remarks>
    /// The IPC parser splits commands on <c>;</c> and <c>,</c> outside quotes, splits arguments on whitespace outside
    /// quotes and then strips every quote character, treating a backslash as an escape that stays in the text. A quote
    /// character can only be embedded without the backslash by wrapping it in the other quote kind, so the value is
    /// emitted as adjacent runs, each inside the quote kind it cannot terminate. Backslashes and control characters
    /// cannot be represented faithfully and are rejected.
    /// </remarks>
    public static bool TryQuoteArgument(string value, [NotNullWhen(true)] out string? quoted)
    {
        quoted = null;
        if (value.Length is 0)
        {
            return false;
        }

        var builder = new StringBuilder(value.Length + 4);
        var open = '\0';
        foreach (var c in value)
        {
            if (c is '\\' || char.IsControl(c))
            {
                return false;
            }

            var required = c switch
            {
                '\'' => '"',
                '"' => '\'',
                _ => open is '\0' ? '"' : open,
            };

            if (required != open)
            {
                if (open is not '\0')
                {
                    _ = builder.Append(open);
                }

                _ = builder.Append(required);
                open = required;
            }

            _ = builder.Append(c);
        }

        _ = builder.Append(open);
        quoted = builder.ToString();
        return true;
    }
}
