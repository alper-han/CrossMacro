using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using CrossMacro.Platform.Windows.Services.Runtime;
using CrossMacro.TestInfrastructure;

namespace CrossMacro.Platform.Windows.Tests.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsTimerResolutionTests(ITestOutputHelper output)
{
    [WindowsFact]
    public async Task EnsureRaised_LetsShortDelaysWakeNearTheirDeadline()
    {
        var before = await AverageDelayMillisecondsAsync();
        WindowsTimerResolution.EnsureRaised();
        var after = await AverageDelayMillisecondsAsync();

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"7 ms delay took {before:F1} ms before and {after:F1} ms after."));
        // The default 15.6 ms timer tick turns every 7 ms delay into about 15.6 ms.
        Assert.InRange(after, 6.0, 12.0);
    }

    private static async Task<double> AverageDelayMillisecondsAsync()
    {
        const int Samples = 60;
        var started = Stopwatch.GetTimestamp();
        for (var sample = 0; sample < Samples; sample++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(7), TimeProvider.System, TestContext.Current.CancellationToken);
        }

        return Stopwatch.GetElapsedTime(started).TotalMilliseconds / Samples;
    }
}
