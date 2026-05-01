namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.Net.Sockets;
using CrossMacro.Daemon;
using CrossMacro.Daemon.Services;

public sealed class ProgramCompositionTests
{
    [Fact]
    public void CreateDaemonService_UsesResolvedSocketPathAndDefersSessionFactory()
    {
        var security = new StubSecurityService();
        var permissionService = new StubLinuxPermissionService();
        var socketPathResolver = new StubDaemonSocketPathResolver("/tmp/test-crossmacro.sock");
        var sessionHandler = new StubSessionHandler();
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);

        var service = Program.CreateDaemonService(
            security,
            permissionService,
            socketPathResolver,
            sessionHandlerFactory);

        Assert.NotNull(service);
        Assert.Equal(1, socketPathResolver.ResolveCalls);
        Assert.Equal(0, sessionHandlerFactory.CreateCalls);
        Assert.Null(sessionHandlerFactory.LastCreated);
    }

    [Fact]
    public void CreateDaemonService_ThrowsForNullSessionHandlerFactory()
    {
        var security = new StubSecurityService();
        var permissionService = new StubLinuxPermissionService();
        var socketPathResolver = new StubDaemonSocketPathResolver("/tmp/test-crossmacro.sock");

        Assert.Throws<ArgumentNullException>(() => Program.CreateDaemonService(
            security,
            permissionService,
            socketPathResolver,
            null!));
    }

    private sealed class RecordingSessionHandlerFactory : ISessionHandlerFactory
    {
        private readonly ISessionHandler _handler;

        public RecordingSessionHandlerFactory(ISessionHandler handler)
        {
            _handler = handler;
        }

        public int CreateCalls { get; private set; }
        public ISessionHandler? LastCreated { get; private set; }

        public ISessionHandler Create()
        {
            CreateCalls++;
            LastCreated = _handler;
            return _handler;
        }
    }

    private sealed class StubDaemonSocketPathResolver : IDaemonSocketPathResolver
    {
        private readonly string _path;

        public StubDaemonSocketPathResolver(string path)
        {
            _path = path;
        }

        public int ResolveCalls { get; private set; }

        public string ResolveSocketPath()
        {
            ResolveCalls++;
            return _path;
        }
    }

    private sealed class StubSessionHandler : ISessionHandler
    {
        public Task RunAsync(Socket client, uint uid, int pid, CancellationToken token)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubSecurityService : ISecurityService
    {
        public Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client)
        {
            throw new NotSupportedException();
        }

        public void LogDisconnect(uint uid, int pid, TimeSpan duration)
        {
        }

        public void LogCaptureStart(uint uid, int pid, bool mouse, bool kb)
        {
        }

        public void LogCaptureStop(uint uid, int pid)
        {
        }

        public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value)
        {
        }
    }

    private sealed class StubLinuxPermissionService : ILinuxPermissionService
    {
        public void ConfigureSocketPermissions(string socketPath)
        {
        }
    }
}
