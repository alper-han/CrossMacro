using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using CrossMacro.Daemon.Security;

namespace CrossMacro.Daemon.Services;

public interface IRateLimiterService
{
    bool IsRateLimited(uint uid);
    void RecordSuccess(uint uid);
}

public interface ISecurityAuditLogger
{
    void LogConnectionAttempt(uint uid, int pid, string? executable, bool success, string? reason = null);
    void LogSecurityViolation(uint uid, int pid, string violation);
    void LogRateLimited(uint uid, int pid);
    void LogDisconnect(uint uid, int pid, TimeSpan duration);
    void LogCaptureStart(uint uid, int pid, bool mouse, bool keyboard);
    void LogCaptureStop(uint uid, int pid);
}

public interface IPeerCredentialsProvider
{
    (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket);
    string? GetProcessExecutable(int pid);
    bool IsUserInGroup(uint uid, string groupName);
}

public interface IPolkitAuthorizationService
{
    Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid);
}

public sealed class RateLimiterService : IRateLimiterService
{
    private readonly RateLimiter _inner;

    public RateLimiterService(RateLimiter inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public bool IsRateLimited(uint uid) => _inner.IsRateLimited(uid);
    public void RecordSuccess(uint uid) => _inner.RecordSuccess(uid);
}

public sealed class SecurityAuditLogger : ISecurityAuditLogger
{
    private readonly AuditLogger _inner;

    public SecurityAuditLogger(AuditLogger inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public void LogConnectionAttempt(uint uid, int pid, string? executable, bool success, string? reason = null) =>
        _inner.LogConnectionAttempt(uid, pid, executable, success, reason);

    public void LogSecurityViolation(uint uid, int pid, string violation) =>
        _inner.LogSecurityViolation(uid, pid, violation);

    public void LogRateLimited(uint uid, int pid) =>
        _inner.LogRateLimited(uid, pid);

    public void LogDisconnect(uint uid, int pid, TimeSpan duration) =>
        _inner.LogDisconnect(uid, pid, duration);

    public void LogCaptureStart(uint uid, int pid, bool mouse, bool keyboard) =>
        _inner.LogCaptureStart(uid, pid, mouse, keyboard);

    public void LogCaptureStop(uint uid, int pid) =>
        _inner.LogCaptureStop(uid, pid);
}

public sealed class PeerCredentialsProvider : IPeerCredentialsProvider
{
    public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket) => PeerCredentials.GetCredentials(socket);
    public string? GetProcessExecutable(int pid) => PeerCredentials.GetProcessExecutable(pid);
    public bool IsUserInGroup(uint uid, string groupName) => PeerCredentials.IsUserInGroup(uid, groupName);
}

public sealed class PolkitAuthorizationService : IPolkitAuthorizationService
{
    public Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid) =>
        PolkitChecker.CheckAuthorizationAsync(uid, pid, PolkitChecker.Actions.InputCapture);
}
