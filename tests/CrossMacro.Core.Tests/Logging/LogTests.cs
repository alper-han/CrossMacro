using System.Globalization;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Tests.Logging;

public sealed class LogTests
{
    [Fact]
    public void BootstrapLogger_SuppressedLevelsStaySilentAndWarningsAnnounceTheFallbackOnce()
    {
        var originalError = Console.Error;
        using var captured = new StringWriter(CultureInfo.InvariantCulture);
        Console.SetError(captured);
        using var scope = Log.PushLogger(null!);
        try
        {
            Log.Debug("suppressed {Value}", 1);
            _ = captured.ToString().Should().BeEmpty();

            Log.Warning("first {Value}", 2);
            Log.Warning("second {Value}", 3);

            var lines = captured.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            _ = lines.Should().HaveCount(3);
            _ = lines[0].Should().Contain("bootstrap fallback");
            _ = lines[1].Should().Contain("first {Value} | 2").And.Contain("[Warning]");
            _ = lines[2].Should().Contain("second {Value} | 3");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
