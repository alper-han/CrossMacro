using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using CrossMacro.Daemon.Services;
using Xunit;

namespace CrossMacro.Daemon.Tests.Services;

public class SecurityServiceTests
{
    [Fact]
    public async Task ValidateConnectionAsync_WhenPeerCredentialsMissing_ShouldRejectAndLogViolation()
    {
        using var socket = CreateSocket();
        var rateLimiter = new FakeRateLimiterService();
        var auditLogger = new FakeAuditLogger();
        var peerCredentials = new FakePeerCredentialsProvider { Credentials = null };
        var polkit = new FakePolkitAuthorizationService { IsAuthorized = true };
        var service = new SecurityService(rateLimiter, auditLogger, peerCredentials, polkit);

        var result = await service.ValidateConnectionAsync(socket);

        Assert.Null(result);
        Assert.Contains("PEER_CRED_FAILED", auditLogger.SecurityViolations);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenRootUser_ShouldReject()
    {
        using var socket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 0, Gid: 0, Pid: 123),
            inGroup: true,
            isAuthorized: true);

        var result = await service.SecurityService.ValidateConnectionAsync(socket);

        Assert.Null(result);
        Assert.Contains(
            service.AuditLogger.ConnectionAttempts,
            x => !x.Success && x.Reason == "ROOT_REJECTED");
        Assert.True(socket.SafeHandle.IsClosed);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenRateLimited_ShouldReject()
    {
        using var socket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1000, Gid: 1000, Pid: 123),
            inGroup: true,
            isAuthorized: true,
            isRateLimited: true);

        var result = await service.SecurityService.ValidateConnectionAsync(socket);

        Assert.Null(result);
        Assert.Single(service.AuditLogger.RateLimitedEvents);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenNotInCrossmacroGroup_ShouldReject()
    {
        using var socket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1000, Gid: 1000, Pid: 123),
            inGroup: false,
            isAuthorized: true);

        var result = await service.SecurityService.ValidateConnectionAsync(socket);

        Assert.Null(result);
        Assert.Contains(
            service.AuditLogger.ConnectionAttempts,
            x => !x.Success && x.Reason == "NOT_IN_GROUP");
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenPolkitDenied_ShouldReject()
    {
        using var socket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1000, Gid: 1000, Pid: 123),
            inGroup: true,
            isAuthorized: false);

        var result = await service.SecurityService.ValidateConnectionAsync(socket);

        Assert.Null(result);
        Assert.Contains(
            service.AuditLogger.ConnectionAttempts,
            x => !x.Success && x.Reason == "POLKIT_DENIED");
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenPolkitThrows_ShouldRejectFailClosed()
    {
        using var socket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1000, Gid: 1000, Pid: 123),
            inGroup: true,
            isAuthorized: true,
            polkitException: new TimeoutException("polkit timeout"));

        var result = await service.SecurityService.ValidateConnectionAsync(socket);

        Assert.Null(result);
        Assert.Contains(
            service.AuditLogger.ConnectionAttempts,
            x => !x.Success && x.Reason == "POLKIT_ERROR");
        Assert.True(socket.SafeHandle.IsClosed);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenAllChecksPass_ShouldReturnIdentityAndRecordSuccess()
    {
        using var socket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1001, Gid: 1001, Pid: 456),
            inGroup: true,
            isAuthorized: true,
            executable: "/usr/bin/crossmacro-ui");

        var result = await service.SecurityService.ValidateConnectionAsync(socket);

        Assert.Equal((1001u, 456), result);
        Assert.Equal(1001u, service.RateLimiter.RecordSuccessUid);
        Assert.Contains(
            service.AuditLogger.ConnectionAttempts,
            x => x.Success && x.Executable == "/usr/bin/crossmacro-ui");
        Assert.False(socket.SafeHandle.IsClosed);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenSameUidReconnectsSoon_ShouldReuseAuthorizationCache()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1000, Gid: 1000, Pid: 123),
            inGroup: true,
            isAuthorized: true);

        var first = await service.SecurityService.ValidateConnectionAsync(firstSocket);
        var second = await service.SecurityService.ValidateConnectionAsync(secondSocket);

        Assert.Equal((1000u, 123), first);
        Assert.Equal((1000u, 123), second);
        Assert.Equal(1, service.Polkit.CallCount);
    }

    private static Socket CreateSocket() =>
        new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

    private static (SecurityService SecurityService, FakeRateLimiterService RateLimiter, FakeAuditLogger AuditLogger, FakePolkitAuthorizationService Polkit) CreateService(
        (uint Uid, uint Gid, int Pid)? credentials,
        bool inGroup,
        bool isAuthorized,
        bool isRateLimited = false,
        string? executable = null,
        Exception? polkitException = null)
    {
        var rateLimiter = new FakeRateLimiterService { IsRateLimitedResult = isRateLimited };
        var auditLogger = new FakeAuditLogger();
        var peerCredentials = new FakePeerCredentialsProvider
        {
            Credentials = credentials,
            IsUserInGroupResult = inGroup,
            Executable = executable
        };
        var polkit = new FakePolkitAuthorizationService
        {
            IsAuthorized = isAuthorized,
            Exception = polkitException
        };

        return (new SecurityService(rateLimiter, auditLogger, peerCredentials, polkit), rateLimiter, auditLogger, polkit);
    }

    private sealed class FakeRateLimiterService : IRateLimiterService
    {
        public bool IsRateLimitedResult { get; init; }
        public uint? RecordSuccessUid { get; private set; }

        public bool IsRateLimited(uint uid) => IsRateLimitedResult;

        public void RecordSuccess(uint uid)
        {
            RecordSuccessUid = uid;
        }
    }

    private sealed class FakeAuditLogger : ISecurityAuditLogger
    {
        public List<string> SecurityViolations { get; } = [];
        public List<(uint Uid, int Pid)> RateLimitedEvents { get; } = [];
        public List<(uint Uid, int Pid, string? Executable, bool Success, string? Reason)> ConnectionAttempts { get; } = [];

        public void LogConnectionAttempt(uint uid, int pid, string? executable, bool success, string? reason = null)
        {
            ConnectionAttempts.Add((uid, pid, executable, success, reason));
        }

        public void LogSecurityViolation(uint uid, int pid, string violation)
        {
            SecurityViolations.Add(violation);
        }

        public void LogRateLimited(uint uid, int pid)
        {
            RateLimitedEvents.Add((uid, pid));
        }

        public void LogDisconnect(uint uid, int pid, TimeSpan duration)
        {
        }

        public void LogCaptureStart(uint uid, int pid, bool mouse, bool keyboard)
        {
        }

        public void LogCaptureStop(uint uid, int pid)
        {
        }
    }

    private sealed class FakePeerCredentialsProvider : IPeerCredentialsProvider
    {
        public (uint Uid, uint Gid, int Pid)? Credentials { get; init; }
        public string? Executable { get; init; }
        public bool IsUserInGroupResult { get; init; }

        public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket) => Credentials;

        public string? GetProcessExecutable(int pid) => Executable;

        public bool IsUserInGroup(uint uid, string groupName) => IsUserInGroupResult;
    }

    private sealed class FakePolkitAuthorizationService : IPolkitAuthorizationService
    {
        public bool IsAuthorized { get; init; }
        public Exception? Exception { get; init; }
        public int CallCount { get; private set; }

        public Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid)
        {
            CallCount++;

            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(IsAuthorized);
        }
    }
}
