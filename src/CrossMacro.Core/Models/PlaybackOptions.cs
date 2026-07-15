
namespace CrossMacro.Core.Models;

/// <summary>
/// Options for macro playback
/// </summary>
public class PlaybackOptions
{
    public const double MinSpeedMultiplier = 0.1;
    public const double MaxSpeedMultiplier = 10.0;
    public const double DefaultSpeedMultiplier = 1.0;
    public const int MinDelayMs = 0;
    public const int DefaultDelayMs = 0;

    /// <summary>
    /// Speed multiplier (1.0 = normal speed, 2.0 = double speed, 0.5 = half speed)
    /// </summary>
    public double SpeedMultiplier { get; set; } = DefaultSpeedMultiplier;

    /// <summary>
    /// Whether to loop the macro continuously
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    /// Number of times to repeat the macro (0 = infinite if Loop is true)
    /// </summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>
    /// Fixed delay between repetitions in milliseconds.
    /// Ignored when <see cref="UseRandomRepeatDelay"/> is enabled.
    /// </summary>
    public int RepeatDelayMs { get; set; } = DefaultDelayMs;

    /// <summary>
    /// Whether to choose a new random delay between repetitions.
    /// </summary>
    public bool UseRandomRepeatDelay { get; set; }

    /// <summary>
    /// Minimum random delay between repetitions in milliseconds.
    /// </summary>
    public int RepeatDelayMinMs { get; set; } = DefaultDelayMs;

    /// <summary>
    /// Maximum random delay between repetitions in milliseconds.
    /// </summary>
    public int RepeatDelayMaxMs { get; set; } = DefaultDelayMs;

    public void Normalize()
    {
        SpeedMultiplier = NormalizeSpeedMultiplier(SpeedMultiplier);
        RepeatDelayMs = NormalizeDelayMs(RepeatDelayMs);
        (RepeatDelayMinMs, RepeatDelayMaxMs) = NormalizeDelayRange(RepeatDelayMinMs, RepeatDelayMaxMs);
    }

    public static double NormalizeSpeedMultiplier(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return DefaultSpeedMultiplier;
        }

        return Math.Clamp(value, MinSpeedMultiplier, MaxSpeedMultiplier);
    }

    public static int NormalizeDelayMs(int value)
    {
        return Math.Max(MinDelayMs, value);
    }

    public static (int Min, int Max) NormalizeDelayRange(int min, int max)
    {
        min = NormalizeDelayMs(min);
        max = NormalizeDelayMs(max);

        if (max < min)
        {
            max = min;
        }

        return (min, max);
    }
}
