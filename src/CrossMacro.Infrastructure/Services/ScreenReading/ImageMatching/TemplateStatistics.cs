using System.Runtime.InteropServices;

using static CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching.MatcherWork;

namespace CrossMacro.Infrastructure.Services.ScreenReading.ImageMatching;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TemplateStatistics(long CoverageSum, double LumaMean, double LumaVariance, double MaximumSad, double Coverage)
{
    public bool HasUsableVariance => LumaVariance >= 16.0 && double.IsFinite(LumaVariance);
    public double EffectivePixelCount => CoverageSum / (double)byte.MaxValue;

    private static double GetMaximumSad(double coverageSum) => coverageSum * ColorChannelCount * MaxChannelDifference;

    public static TemplateStatistics Create(RgbImage template)
    {
        long coverageSum = 0;
        double lumaSum = 0;
        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
            {
                var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                if (coverage is 0)
                {
                    continue;
                }

                coverageSum = SaturatingAdd(coverageSum, coverage);
                lumaSum += coverage * MatchScoring.GetLuma(template, x, y);
            }
        }

        if (coverageSum is 0)
        {
            return new TemplateStatistics(0, 0.0, 0.0, 0.0, 0.0);
        }

        var mean = lumaSum / coverageSum;
        double variance = 0;
        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
            {
                var coverage = template.AlphaMask is null ? byte.MaxValue : template.AlphaMask[(y * template.Width) + x];
                if (coverage is not 0)
                {
                    var difference = MatchScoring.GetLuma(template, x, y) - mean;
                    variance += coverage * difference * difference;
                }
            }
        }

        return new TemplateStatistics(coverageSum, mean, variance / coverageSum, GetMaximumSad(coverageSum), coverageSum / ((double)template.Width * template.Height * byte.MaxValue));
    }
}
