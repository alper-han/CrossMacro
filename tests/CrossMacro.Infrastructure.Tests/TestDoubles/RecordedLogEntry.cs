using System.Globalization;
using CrossMacro.Core.Logging;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

internal sealed record RecordedLogEntry(
    CoreLogLevel Level,
    string Template,
    object?[] Properties,
    Exception? Exception)
{
    public bool Mentions(string text)
    {
        if (Template.Contains(text, StringComparison.Ordinal))
        {
            return true;
        }

        if (Exception?.ToString().Contains(text, StringComparison.Ordinal) is true)
        {
            return true;
        }

        return Properties.Any(property =>
            Convert.ToString(property, CultureInfo.InvariantCulture)?.Contains(text, StringComparison.Ordinal) is true);
    }
}
