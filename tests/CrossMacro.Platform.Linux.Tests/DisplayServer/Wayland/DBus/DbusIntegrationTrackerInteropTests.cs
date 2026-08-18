
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

[Collection(nameof(DbusIntegrationSerialCollection))]
public sealed class DbusIntegrationTrackerInteropTests : DbusIntegrationTestBase
{
    [DbusSessionFact]
    public async Task DbusIntegration_UniqueDestinations_ShouldIsolateTrackerCallbacksAcrossConnections()
    {
        var firstPosition = (X: 0, Y: 0);
        var secondPosition = (X: 0, Y: 0);

        await using var bus = await CreatePrivateSessionBusAsync();
        using var firstServiceConnection = bus.CreateConnection();
        using var secondServiceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await firstServiceConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);
        await secondServiceConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);
        await clientConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);

        firstServiceConnection.AddMethodHandler(new KdeTrackerServiceMethodHandler(new KdeTrackerService(
            (x, y) => firstPosition = (x, y),
            (_, _) => { })));
        secondServiceConnection.AddMethodHandler(new KdeTrackerServiceMethodHandler(new KdeTrackerService(
            (x, y) => secondPosition = (x, y),
            (_, _) => { })));

        var firstDestination = LinuxDbusTransportBoundary.GetUniqueDestination(firstServiceConnection);
        var secondDestination = LinuxDbusTransportBoundary.GetUniqueDestination(secondServiceConnection);
        Assert.NotEqual(firstDestination, secondDestination);

        await new KdeTrackerClient(clientConnection, firstDestination)
            .UpdatePositionAsync(120, 240)
            .WaitAsync(SessionBusTimeout);
        await new KdeTrackerClient(clientConnection, secondDestination)
            .UpdatePositionAsync(360, 480)
            .WaitAsync(SessionBusTimeout);

        Assert.Equal((120, 240), firstPosition);
        Assert.Equal((360, 480), secondPosition);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_TrackerServiceRegistrationAndClientRoundTrip_ShouldInvokeExportedHandlers()
    {
        var position = (X: 0, Y: 0);
        var resolution = (Width: 0, Height: 0);

        await using var bus = await CreatePrivateSessionBusAsync();
        using var serviceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await serviceConnection.ConnectAsync().AsTask()
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);
        await clientConnection.ConnectAsync().AsTask()
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        var service = new KdeTrackerService(
            (x, y) => position = (x, y),
            (width, height) => resolution = (width, height));

        serviceConnection.AddMethodHandler(new KdeTrackerServiceMethodHandler(service));

        await serviceConnection
            .RequestNameAsync(LinuxDbusTransportBoundary.TrackerServiceName, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout);

        var client = new KdeTrackerClient(clientConnection);
        var serviceDisconnectedTask = serviceConnection.DisconnectedAsync();

        try
        {
            await client.UpdatePositionAsync(120, 240).WaitAsync(SessionBusTimeout);
        }
        catch (Exception ex)
        {
            Exception? serviceException = null;

            try
            {
                serviceException = await serviceDisconnectedTask.WaitAsync(SessionBusTimeout);
            }
            catch (Exception waitFailure)
            {
                Debug.WriteLine(waitFailure);
            }

            if (serviceException is not null)
            {
                throw new XunitException($"Service connection disconnected: {serviceException}", ex);
            }

            throw;
        }

        await client.UpdateResolutionAsync(1920, 1080).WaitAsync(SessionBusTimeout);

        Assert.Equal((120, 240), position);
        Assert.Equal((1920, 1080), resolution);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_TrackerService_ShouldRejectWrongInterfaceWithoutInvokingCallbacks()
    {
        var position = (X: 0, Y: 0);

        await using var bus = await CreatePrivateSessionBusAsync();
        using var serviceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await serviceConnection.ConnectAsync();
        await clientConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);

        var service = new KdeTrackerService(
            (x, y) => position = (x, y),
            (_, _) => { });

        serviceConnection.AddMethodHandler(new KdeTrackerServiceMethodHandler(service));
        await serviceConnection
            .RequestNameAsync(LinuxDbusTransportBoundary.TrackerServiceName, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout);

        var wrongInterfaceRequest = clientConnection.GetMessageWriter();
        wrongInterfaceRequest.WriteMethodCallHeader(
            destination: LinuxDbusTransportBoundary.TrackerServiceName,
            path: KdeTrackerService.TrackerObjectPath,
            @interface: "wrong.iface",
            member: KdeTrackerService.UpdatePositionMethod,
            signature: "ii");
        wrongInterfaceRequest.WriteInt32(120);
        wrongInterfaceRequest.WriteInt32(240);
        var wrongInterfaceMessage = wrongInterfaceRequest.CreateMessage();

        var exception = await Assert.ThrowsAnyAsync<DBusErrorReplyException>(() =>
            clientConnection.CallMethodAsync(wrongInterfaceMessage).WaitAsync(SessionBusTimeout));

        Assert.Equal("org.freedesktop.DBus.Error.UnknownMethod", exception.ErrorName);
        Assert.Equal((0, 0), position);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_TrackerService_ShouldRejectInvalidSignatureWithoutInvokingCallbacks()
    {
        var position = (X: 0, Y: 0);

        await using var bus = await CreatePrivateSessionBusAsync();
        using var serviceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await serviceConnection.ConnectAsync();
        await clientConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);

        var service = new KdeTrackerService(
            (x, y) => position = (x, y),
            (_, _) => { });

        serviceConnection.AddMethodHandler(new KdeTrackerServiceMethodHandler(service));
        await serviceConnection
            .RequestNameAsync(LinuxDbusTransportBoundary.TrackerServiceName, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout);

        var invalidSignatureRequest = clientConnection.GetMessageWriter();
        invalidSignatureRequest.WriteMethodCallHeader(
            destination: LinuxDbusTransportBoundary.TrackerServiceName,
            path: KdeTrackerService.TrackerObjectPath,
            @interface: KdeTrackerService.TrackerInterface,
            member: KdeTrackerService.UpdatePositionMethod,
            signature: "s");
        invalidSignatureRequest.WriteString("oops");
        var invalidSignatureMessage = invalidSignatureRequest.CreateMessage();

        var exception = await Assert.ThrowsAnyAsync<DBusErrorReplyException>(() =>
            clientConnection.CallMethodAsync(invalidSignatureMessage).WaitAsync(SessionBusTimeout));

        Assert.Equal("org.freedesktop.DBus.Error.InvalidArgs", exception.ErrorName);
        Assert.Equal((0, 0), position);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_GnomeExtensionsClient_ShouldSendUuidAndParseReply()
    {
        const string expectedUuid = "crossmacro@zynix.net";
        string? receivedUuid = null;

        await using var bus = await CreatePrivateSessionBusAsync();
        using var serviceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await serviceConnection.ConnectAsync();
        await clientConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);

        serviceConnection.AddMethodHandler(new RecordingMethodHandler(
            GnomeShellExtensionsClient.Path,
            GnomeShellExtensionsClient.Interface,
            "GetExtensionInfo",
            request =>
            {
                receivedUuid = request.GetBodyReader().ReadString();
            },
            "a{sv}",
            (ref MessageWriter writer) =>
            {
                var dictStart = writer.WriteDictionaryStart();
                writer.WriteString("state");
                writer.WriteVariantUInt32(1);
                writer.WriteDictionaryEnd(dictStart);
            }));

        await serviceConnection.RequestNameAsync(GnomeShellExtensionsClient.Service, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout);

        var client = new GnomeShellExtensionsClient(clientConnection);
        var info = await client.GetExtensionInfoAsync(expectedUuid).WaitAsync(SessionBusTimeout);

        Assert.Equal(expectedUuid, receivedUuid);
        Assert.Equal((uint)1, info["state"]);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_KWinScriptingClient_ShouldSendPathAndPluginNameForLoad()
    {
        const string expectedPath = "/tmp/crossmacro-tracker.js";
        const string expectedPluginName = "io.github.alper_han.crossmacro.position.test";
        string? receivedPath = null;
        string? receivedPluginName = null;

        await using var bus = await CreatePrivateSessionBusAsync();
        using var serviceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await serviceConnection.ConnectAsync();
        await clientConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);

        serviceConnection.AddMethodHandler(new RecordingMethodHandler(
            KWinScriptingClient.Path,
            KWinScriptingClient.Interface,
            "loadScript",
            request =>
            {
                Assert.Equal("ss", request.SignatureAsString);
                var reader = request.GetBodyReader();
                receivedPath = reader.ReadString();
                receivedPluginName = reader.ReadString();
            },
            "i",
            (ref MessageWriter writer) => writer.WriteInt32(42)));

        await serviceConnection.RequestNameAsync(KWinScriptingClient.Service, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        var client = new KWinScriptingClient(clientConnection);
        var scriptId = await client.LoadScriptAsync(expectedPath, expectedPluginName)
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        Assert.Equal(42, scriptId);
        Assert.Equal(expectedPath, receivedPath);
        Assert.Equal(expectedPluginName, receivedPluginName);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_KWinScriptingClient_ShouldSendPluginNameForUnload()
    {
        const string expectedScriptName = "io.github.alper_han.crossmacro.position.test";
        string? receivedScriptName = null;

        await using var bus = await CreatePrivateSessionBusAsync();
        using var serviceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await serviceConnection.ConnectAsync().AsTask()
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);
        await clientConnection.ConnectAsync().AsTask()
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        serviceConnection.AddMethodHandler(new RecordingMethodHandler(
            KWinScriptingClient.Path,
            KWinScriptingClient.Interface,
            "unloadScript",
            request =>
            {
                receivedScriptName = request.GetBodyReader().ReadString();
            },
            "b",
            (ref MessageWriter writer) => writer.WriteBool(true)));

        await serviceConnection.RequestNameAsync(KWinScriptingClient.Service, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        var client = new KWinScriptingClient(clientConnection);
        await client.UnloadScriptAsync(expectedScriptName)
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        Assert.Equal(expectedScriptName, receivedScriptName);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_KWinScriptClient_ShouldRouteRunByNumericId()
    {
        var runReceived = false;

        await using var bus = await CreatePrivateSessionBusAsync();
        using var serviceConnection = bus.CreateConnection();
        using var clientConnection = bus.CreateConnection();

        await serviceConnection.ConnectAsync().AsTask()
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);
        await clientConnection.ConnectAsync().AsTask()
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        serviceConnection.AddMethodHandler(new RecordingMethodHandler(
            "/Scripting/Script42",
            KWinScriptClient.Interface,
            "run",
            _ => runReceived = true,
            replySignature: null,
            writeReply: null));

        await serviceConnection.RequestNameAsync(KWinScriptClient.Service, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        await new KWinScriptClient(clientConnection, 42).RunAsync()
            .WaitAsync(SessionBusTimeout, TimeProvider.System, CancellationToken.None);

        Assert.True(runReceived);
    }

    private delegate void ReplyWriter(ref MessageWriter writer);

    private sealed class RecordingMethodHandler(
        string path,
        string expectedInterface,
        string expectedMember,
        Action<Message> onRequest,
        string? replySignature,
DbusIntegrationTrackerInteropTests.ReplyWriter? writeReply) : IPathMethodHandler
    {
        private readonly string _expectedInterface = expectedInterface;
        private readonly string _expectedMember = expectedMember;
        private readonly Action<Message> _onRequest = onRequest;
        private readonly string? _replySignature = replySignature;
        private readonly ReplyWriter? _writeReply = writeReply;

        public string Path { get; } = path;

        public bool HandlesChildPaths => false;

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            try
            {
                var request = context.Request;
                if (!string.Equals(request.InterfaceAsString, _expectedInterface, StringComparison.Ordinal)
                    || !string.Equals(request.MemberAsString, _expectedMember, StringComparison.Ordinal))
                {
                    context.ReplyUnknownMethodError();
                    return default;
                }

                _onRequest(request);

                var writer = context.CreateReplyWriter(_replySignature);
                try
                {
                    _writeReply?.Invoke(ref writer);
                    context.Reply(writer.CreateMessage());
                }
                finally
                {
                    writer.Dispose();
                }
            }
            catch (Exception ex)
            {
                context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message);
            }

            return default;
        }
    }
}
