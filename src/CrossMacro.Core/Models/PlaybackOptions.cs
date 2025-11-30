namespace CrossMacro.Core.Models;

/// <summary>
/// Options for macro playback
/// </summary>
public class PlaybackOptions
{
    /// <summary>
    /// Speed multiplier (1.0 = normal speed, 2.0 = double speed, 0.5 = half speed)
    /// </summary>
    public double SpeedMultiplier { get; set; } = 1.0;
    
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
}
