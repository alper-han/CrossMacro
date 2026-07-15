namespace CrossMacro.Core.Models;

/// <summary>
/// Defines how direct typing should inject keyboard input.
/// </summary>
public enum DirectTypingMethod
{
    /// <summary>
    /// Prefer the fast batched input path when supported.
    /// </summary>
    FastBatch,

    /// <summary>
    /// Send each key separately for better compatibility on sensitive input stacks.
    /// </summary>
    CompatibleKeyByKey,
}
