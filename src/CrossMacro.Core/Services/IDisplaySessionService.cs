namespace CrossMacro.Core.Services
{
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
        bool IsSessionSupported(out string reason);
    }
}
