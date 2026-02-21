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

    private double _speedMultiplier = DefaultSpeedMultiplier;

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
    /// Delay between repetitions in milliseconds
    /// </summary>
    public int RepeatDelayMs { get; set; } = 0;

    public static double NormalizeSpeedMultiplier(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return DefaultSpeedMultiplier;
        }

        return Math.Clamp(value, MinSpeedMultiplier, MaxSpeedMultiplier);
    }
}
