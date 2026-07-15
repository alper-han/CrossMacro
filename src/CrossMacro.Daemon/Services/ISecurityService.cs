
namespace CrossMacro.Daemon.Services;

/// <summary>
/// Handles authentication, authorization, rate limiting, and audit logging for daemon connections.
/// </summary>
public interface ISecurityService
{
    /// <summary>
    /// Validates a new client connection.
    /// Performs PeerCreds check, Root check, Rate Limit check, Group check, and Polkit check.
    /// </summary>
    /// <returns>Tuple of (UID, PID) if authorized; null if rejected.</returns>
    public Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client);

    /// <summary>
    /// Logs a disconnection event.
    /// </summary>
    public void LogDisconnect(uint uid, int pid, TimeSpan duration);

    /// <summary>
    /// Logs a capture start event.
    /// </summary>
    public void LogCaptureStart(uint uid, int pid, bool mouse, bool kb);

    /// <summary>
    /// Logs a capture stop event.
    /// </summary>
    public void LogCaptureStop(uint uid, int pid);

    /// <summary>
    /// Logs a simulated input event.
    /// </summary>
    public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value);

}
