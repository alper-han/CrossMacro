
namespace CrossMacro.Daemon.Services;

public interface ISecurityAuditLogger
{
    public void LogConnectionAttempt(uint uid, int pid, string? executable, bool success, string? reason = null);
    public void LogSecurityViolation(uint uid, int pid, string violation);
    public void LogRateLimited(uint uid, int pid);
    public void LogDisconnect(uint uid, int pid, TimeSpan duration);
    public void LogCaptureStart(uint uid, int pid, bool mouse, bool keyboard);
    public void LogCaptureStop(uint uid, int pid);
    public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value);
}
