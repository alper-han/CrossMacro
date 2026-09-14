namespace CrossMacro.Platform.Abstractions.Runtime;

/// <summary>
/// Service for checking if the current display session (e.g. X11, Wayland) is supported.
/// </summary>
public interface IDisplaySessionService
{
    /// <summary>
    /// Checks if the current session is supported for running the application.
    /// </summary>
    /// <param name="reason">The reason why the session is not supported, if applicable.</param>
    /// <returns>True if the session is supported; otherwise, false.</returns>
    public bool IsSessionSupported(out string reason);

    public ValueTask<(bool Supported, string Reason)> IsSessionSupportedAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<(bool Supported, string Reason)>(cancellationToken);
        }

        var supported = IsSessionSupported(out var reason);
        return ValueTask.FromResult((supported, reason));
    }
}
