using System;

namespace CrossMacro.Daemon.Services;

public interface ISecurityAuditLogger
{
    void LogConnectionAttempt(uint uid, int pid, string? executable, bool success, string? reason = null);
    void LogSecurityViolation(uint uid, int pid, string violation);
    void LogRateLimited(uint uid, int pid);
    void LogDisconnect(uint uid, int pid, TimeSpan duration);
    void LogCaptureStart(uint uid, int pid, bool mouse, bool keyboard);
    void LogCaptureStop(uint uid, int pid);
    void LogSimulation(uint uid, int pid, ushort type, ushort code, int value);
}
