
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
    public const int DefaultStrictSpeedMotionEventsPerSecond = 1_000;
    public const int MinStrictSpeedMotionEventsPerSecond = 60;
    public const int MaxStrictSpeedMotionEventsPerSecond = 10_000;
    public const int DefaultPrecisionMotionEventsPerSecond = 300;
    public const int MinPrecisionMotionEventsPerSecond = 60;
    public const int MaxPrecisionMotionEventsPerSecond = 10_000;
    public const double DefaultMaximumMotionErrorPixels = 2d;
    public const double MinMaximumMotionErrorPixels = 0.25d;
    public const double MaxMaximumMotionErrorPixels = 500d;

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

    /// <summary>Controls the trade-off between pointer fidelity and requested duration.</summary>
    public MotionPlaybackMode MotionMode { get; set; } = MotionPlaybackMode.Precision;

    /// <summary>Maximum injected pointer reports per second in StrictSpeed mode; zero uses the default.</summary>
    public int StrictSpeedMotionEventsPerSecond { get; set; } = DefaultStrictSpeedMotionEventsPerSecond;

    /// <summary>Precision output ceiling; playback slows down instead of dropping positions.</summary>
    public int PrecisionMotionEventsPerSecond { get; set; } = DefaultPrecisionMotionEventsPerSecond;

    /// <summary>Maximum pixel error allowed when StrictSpeed simplifies a trajectory.</summary>
    public double MaximumMotionErrorPixels { get; set; } = DefaultMaximumMotionErrorPixels;

    public void Normalize()
    {
        SpeedMultiplier = NormalizeSpeedMultiplier(SpeedMultiplier);
        RepeatDelayMs = NormalizeDelayMs(RepeatDelayMs);
        (RepeatDelayMinMs, RepeatDelayMaxMs) = NormalizeDelayRange(RepeatDelayMinMs, RepeatDelayMaxMs);
        MotionMode = Enum.IsDefined(MotionMode)
            ? MotionMode
            : MotionPlaybackMode.Precision;
        StrictSpeedMotionEventsPerSecond = NormalizeStrictSpeedMotionEventsPerSecond(StrictSpeedMotionEventsPerSecond);
        PrecisionMotionEventsPerSecond = NormalizePrecisionMotionEventsPerSecond(PrecisionMotionEventsPerSecond);
        MaximumMotionErrorPixels = NormalizeMaximumMotionErrorPixels(MaximumMotionErrorPixels);
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

    public static int NormalizeStrictSpeedMotionEventsPerSecond(int value)
    {
        if (value <= 0)
        {
            return DefaultStrictSpeedMotionEventsPerSecond;
        }

        return Math.Clamp(
            value,
            MinStrictSpeedMotionEventsPerSecond,
            MaxStrictSpeedMotionEventsPerSecond);
    }

    public static int NormalizePrecisionMotionEventsPerSecond(int value)
    {
        if (value <= 0)
        {
            return DefaultPrecisionMotionEventsPerSecond;
        }

        return Math.Clamp(
            value,
            MinPrecisionMotionEventsPerSecond,
            MaxPrecisionMotionEventsPerSecond);
    }

    public static double NormalizeMaximumMotionErrorPixels(double value)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            return DefaultMaximumMotionErrorPixels;
        }

        return Math.Clamp(value, MinMaximumMotionErrorPixels, MaxMaximumMotionErrorPixels);
    }
}
