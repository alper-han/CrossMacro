using System;

namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for abstracting time operations to facilitate testing
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current local date and time
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets the current UTC date and time
    /// </summary>
    DateTime UtcNow { get; }
}
