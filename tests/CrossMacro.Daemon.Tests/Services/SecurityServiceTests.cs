using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection;
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
        Assert.Equal([1000u], service.RateLimiter.IsRateLimitedCalls);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenOnlyOneUidIsRateLimited_ShouldApplyRateLimitPerUid()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var rateLimiter = new FakeRateLimiterService();
        rateLimiter.RateLimitedUids.Add(1000);
        var auditLogger = new FakeAuditLogger();
        var peerCredentials = new SequencePeerCredentialsProvider(
            (Uid: 1000, Gid: 1000, Pid: 123),
            (Uid: 1001, Gid: 1001, Pid: 456));
        var polkit = new SequencePolkitAuthorizationService(true);
        var service = new SecurityService(rateLimiter, auditLogger, peerCredentials, polkit);

        var rateLimitedResult = await service.ValidateConnectionAsync(firstSocket);
        var allowedResult = await service.ValidateConnectionAsync(secondSocket);

        Assert.Null(rateLimitedResult);
        Assert.Equal((1001u, 456), allowedResult);
        Assert.Equal([1000u, 1001u], rateLimiter.IsRateLimitedCalls);
        Assert.Single(auditLogger.RateLimitedEvents, x => x.Uid == 1000u && x.Pid == 123);
        Assert.Equal(1, polkit.CallCount);
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
        Assert.True(socket.SafeHandle.IsClosed);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenPolkitDeniesCachedUid_ShouldRemoveAuthorizationAndRemainNonPermissive()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1000, Gid: 1000, Pid: 123),
            inGroup: true,
            isAuthorized: true);

        var first = await service.SecurityService.ValidateConnectionAsync(firstSocket);
        ExpireAuthorizationCacheEntry(service.SecurityService, 1000);
        service.Polkit.IsAuthorized = false;
        var second = await service.SecurityService.ValidateConnectionAsync(secondSocket);

        Assert.Equal((1000u, 123), first);
        Assert.Null(second);
        Assert.Equal(2, service.Polkit.CallCount);
        Assert.False(HasCachedAuthorization(service.SecurityService, 1000));
        Assert.Contains(
            service.AuditLogger.ConnectionAttempts,
            x => !x.Success && x.Reason == "POLKIT_DENIED");
        Assert.True(secondSocket.SafeHandle.IsClosed);
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
    public void LogSimulation_ForwardsToAuditLogger()
    {
        var service = CreateService(
            credentials: (Uid: 1001, Gid: 1001, Pid: 456),
            inGroup: true,
            isAuthorized: true);

        service.SecurityService.LogSimulation(uid: 1001, pid: 456, type: 1, code: 2, value: 3);

        Assert.Equal(
            [(1001u, 456, (ushort)1, (ushort)2, 3)],
            service.AuditLogger.SimulationEvents);
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

    [Fact]
    public async Task ValidateConnectionAsync_WhenSameUidReconnectsWithDifferentPid_ShouldReuseUidAuthorizationCache()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var peerCredentials = new SequencePeerCredentialsProvider(
            (Uid: 1000, Gid: 1000, Pid: 123),
            (Uid: 1000, Gid: 1000, Pid: 456));
        var polkit = new SequencePolkitAuthorizationService(true);
        var service = new SecurityService(
            new FakeRateLimiterService(),
            new FakeAuditLogger(),
            peerCredentials,
            polkit);

        var first = await service.ValidateConnectionAsync(firstSocket);
        var second = await service.ValidateConnectionAsync(secondSocket);

        Assert.Equal((1000u, 123), first);
        Assert.Equal((1000u, 456), second);
        Assert.Equal(1, polkit.CallCount);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenDifferentUidConnects_ShouldNotReuseAnotherUsersAuthorizationCache()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var peerCredentials = new SequencePeerCredentialsProvider(
            (Uid: 1000, Gid: 1000, Pid: 123),
            (Uid: 1001, Gid: 1001, Pid: 456));
        var polkit = new SequencePolkitAuthorizationService(true, true);
        var service = new SecurityService(
            new FakeRateLimiterService(),
            new FakeAuditLogger(),
            peerCredentials,
            polkit);

        var first = await service.ValidateConnectionAsync(firstSocket);
        var second = await service.ValidateConnectionAsync(secondSocket);

        Assert.Equal((1000u, 123), first);
        Assert.Equal((1001u, 456), second);
        Assert.Equal(2, polkit.CallCount);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenCachedUidLosesGroupMembership_ShouldRejectWithoutReusingPrivilege()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var rateLimiter = new FakeRateLimiterService();
        var auditLogger = new FakeAuditLogger();
        var peerCredentials = new MutablePeerCredentialsProvider
        {
            Credentials = (Uid: 1000, Gid: 1000, Pid: 123),
            IsUserInGroupResult = true
        };
        var polkit = new SequencePolkitAuthorizationService(true);
        var service = new SecurityService(rateLimiter, auditLogger, peerCredentials, polkit);

        var first = await service.ValidateConnectionAsync(firstSocket);
        peerCredentials.IsUserInGroupResult = false;
        var second = await service.ValidateConnectionAsync(secondSocket);

        Assert.Equal((1000u, 123), first);
        Assert.Null(second);
        Assert.Equal(2, peerCredentials.IsUserInGroupCallCount);
        Assert.Equal(1, polkit.CallCount);
        Assert.Contains(
            auditLogger.ConnectionAttempts,
            x => !x.Success && x.Reason == "NOT_IN_GROUP");
        Assert.True(secondSocket.SafeHandle.IsClosed);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenPolkitDenies_ShouldNotCacheAuthorizationForLaterConnections()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var peerCredentials = new MutablePeerCredentialsProvider
        {
            Credentials = (Uid: 1000, Gid: 1000, Pid: 123),
            IsUserInGroupResult = true
        };
        var polkit = new SequencePolkitAuthorizationService(false, true);
        var service = new SecurityService(
            new FakeRateLimiterService(),
            new FakeAuditLogger(),
            peerCredentials,
            polkit);

        var first = await service.ValidateConnectionAsync(firstSocket);
        var second = await service.ValidateConnectionAsync(secondSocket);

        Assert.Null(first);
        Assert.Equal((1000u, 123), second);
        Assert.Equal(2, polkit.CallCount);
        Assert.False(HasCachedAuthorization(service, 9999));
        Assert.True(HasCachedAuthorization(service, 1000));
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenPolkitThrows_ShouldNotCacheAuthorizationForLaterConnections()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var peerCredentials = new MutablePeerCredentialsProvider
        {
            Credentials = (Uid: 1000, Gid: 1000, Pid: 123),
            IsUserInGroupResult = true
        };
        var polkit = new SequencePolkitAuthorizationService(new TimeoutException("polkit timeout"), true);
        var service = new SecurityService(
            new FakeRateLimiterService(),
            new FakeAuditLogger(),
            peerCredentials,
            polkit);

        var first = await service.ValidateConnectionAsync(firstSocket);
        var second = await service.ValidateConnectionAsync(secondSocket);

        Assert.Null(first);
        Assert.Equal((1000u, 123), second);
        Assert.Equal(2, polkit.CallCount);
        Assert.True(HasCachedAuthorization(service, 1000));
    }

    [Fact]
    public async Task ValidateConnectionAsync_WhenCachedAuthorizationExpires_ShouldRequireFreshPolkitAuthorization()
    {
        using var firstSocket = CreateSocket();
        using var secondSocket = CreateSocket();
        var service = CreateService(
            credentials: (Uid: 1000, Gid: 1000, Pid: 123),
            inGroup: true,
            isAuthorized: true);

        var first = await service.SecurityService.ValidateConnectionAsync(firstSocket);
        ExpireAuthorizationCacheEntry(service.SecurityService, 1000);
        var second = await service.SecurityService.ValidateConnectionAsync(secondSocket);

        Assert.Equal((1000u, 123), first);
        Assert.Equal((1000u, 123), second);
        Assert.Equal(2, service.Polkit.CallCount);
    }

    private static Socket CreateSocket() =>
        new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

    private static bool HasCachedAuthorization(SecurityService service, uint uid)
    {
        var cacheField = typeof(SecurityService).GetField("_authorizedUidCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cacheField);

        var cache = cacheField.GetValue(service) as Dictionary<uint, DateTime>;
        Assert.NotNull(cache);

        return cache.ContainsKey(uid);
    }

    private static void ExpireAuthorizationCacheEntry(SecurityService service, uint uid)
    {
        var cacheField = typeof(SecurityService).GetField("_authorizedUidCache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cacheField);

        var cache = cacheField.GetValue(service) as Dictionary<uint, DateTime>;
        Assert.NotNull(cache);
        cache[uid] = DateTime.UtcNow - TimeSpan.FromSeconds(1);
    }

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
        public HashSet<uint> RateLimitedUids { get; } = [];
        public List<uint> IsRateLimitedCalls { get; } = [];
        public uint? RecordSuccessUid { get; private set; }

        public bool IsRateLimited(uint uid)
        {
            IsRateLimitedCalls.Add(uid);
            return IsRateLimitedResult || RateLimitedUids.Contains(uid);
        }

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
        public List<(uint Uid, int Pid, ushort Type, ushort Code, int Value)> SimulationEvents { get; } = [];

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

        public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value)
        {
            SimulationEvents.Add((uid, pid, type, code, value));
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

    private sealed class MutablePeerCredentialsProvider : IPeerCredentialsProvider
    {
        public (uint Uid, uint Gid, int Pid)? Credentials { get; set; }
        public string? Executable { get; set; }
        public bool IsUserInGroupResult { get; set; }
        public int IsUserInGroupCallCount { get; private set; }

        public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket) => Credentials;

        public string? GetProcessExecutable(int pid) => Executable;

        public bool IsUserInGroup(uint uid, string groupName)
        {
            IsUserInGroupCallCount++;
            return IsUserInGroupResult;
        }
    }

    private sealed class SequencePeerCredentialsProvider : IPeerCredentialsProvider
    {
        private readonly Queue<(uint Uid, uint Gid, int Pid)> _credentials;

        public SequencePeerCredentialsProvider(params (uint Uid, uint Gid, int Pid)[] credentials)
        {
            _credentials = new Queue<(uint Uid, uint Gid, int Pid)>(credentials);
        }

        public (uint Uid, uint Gid, int Pid)? GetCredentials(Socket socket)
        {
            Assert.NotEmpty(_credentials);
            return _credentials.Dequeue();
        }

        public string? GetProcessExecutable(int pid) => null;

        public bool IsUserInGroup(uint uid, string groupName) => true;
    }

    private sealed class FakePolkitAuthorizationService : IPolkitAuthorizationService
    {
        public bool IsAuthorized { get; set; }
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

    private sealed class SequencePolkitAuthorizationService : IPolkitAuthorizationService
    {
        private readonly Queue<object> _results;

        public SequencePolkitAuthorizationService(params object[] results)
        {
            _results = new Queue<object>(results);
        }

        public int CallCount { get; private set; }

        public Task<bool> IsInputCaptureAuthorizedAsync(uint uid, int pid)
        {
            CallCount++;
            Assert.NotEmpty(_results);

            var next = _results.Dequeue();
            return next switch
            {
                Exception exception => throw exception,
                bool isAuthorized => Task.FromResult(isAuthorized),
                _ => throw new InvalidOperationException($"Unsupported polkit result type: {next.GetType().FullName}")
            };
        }
    }
}
