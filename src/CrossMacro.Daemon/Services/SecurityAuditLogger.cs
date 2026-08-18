
namespace CrossMacro.Daemon.Services;

internal sealed class SecurityAuditLogger(AuditLogger inner) : ISecurityAuditLogger
{
    private readonly AuditLogger _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public void LogConnectionAttempt(uint uid, int pid, string? executable, bool success, string? reason = null) => _inner.LogConnectionAttempt(uid, pid, executable, success, reason);
    public void LogSecurityViolation(uint uid, int pid, string violation) => _inner.LogSecurityViolation(uid, pid, violation);
    public void LogRateLimited(uint uid, int pid) => _inner.LogRateLimited(uid, pid);
    public void LogDisconnect(uint uid, int pid, TimeSpan duration) => _inner.LogDisconnect(uid, pid, duration);
    public void LogCaptureStart(uint uid, int pid, bool mouse, bool keyboard) => _inner.LogCaptureStart(uid, pid, mouse, keyboard);
    public void LogCaptureStop(uint uid, int pid) => _inner.LogCaptureStop(uid, pid);
    public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value) => _inner.LogSimulation(uid, pid, type, code, value);
    public void Dispose() => _inner.Dispose();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
