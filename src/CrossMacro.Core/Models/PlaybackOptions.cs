using System;

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

    private double _speedMultiplier = DefaultSpeedMultiplier;
    private int _repeatDelayMs = DefaultDelayMs;
    private int _repeatDelayMinMs = DefaultDelayMs;
    private int _repeatDelayMaxMs = DefaultDelayMs;

    /// <summary>
    /// Speed multiplier (1.0 = normal speed, 2.0 = double speed, 0.5 = half speed)
    /// </summary>
    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set => _speedMultiplier = NormalizeSpeedMultiplier(value);
    }

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
    public int RepeatDelayMs
    {
        get => _repeatDelayMs;
        set => _repeatDelayMs = NormalizeDelayMs(value);
    }

    /// <summary>
    /// Whether to choose a new random delay between repetitions.
    /// </summary>
    public bool UseRandomRepeatDelay { get; set; }

    /// <summary>
    /// Minimum random delay between repetitions in milliseconds.
    /// </summary>
    public int RepeatDelayMinMs
    {
        get => _repeatDelayMinMs;
        set
        {
            var normalized = NormalizeDelayMs(value);
            _repeatDelayMinMs = normalized;
            if (_repeatDelayMaxMs < normalized)
            {
                _repeatDelayMaxMs = normalized;
            }
        }
    }

    /// <summary>
    /// Maximum random delay between repetitions in milliseconds.
    /// </summary>
    public int RepeatDelayMaxMs
    {
        get => _repeatDelayMaxMs;
        set
        {
            var normalized = NormalizeDelayMs(value);
            _repeatDelayMaxMs = Math.Max(normalized, _repeatDelayMinMs);
        }
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
