using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using CrossMacro.TestInfrastructure;
using Tmds.DBus.Protocol;
using Xunit.Sdk;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

#pragma warning disable CS0618
[Collection(nameof(DbusIntegrationSerialCollection))]
public sealed class DbusIntegrationTrackerInteropTests : DbusIntegrationTestBase
{
    [DbusSessionFact]
    public async Task DbusIntegration_TrackerServiceRegistrationAndClientRoundTrip_ShouldInvokeExportedHandlers()
    {
        var position = (X: 0, Y: 0);
        var resolution = (Width: 0, Height: 0);

        using var serviceConnection = CreateSessionConnection();
        using var clientConnection = CreateSessionConnection();

        await serviceConnection.ConnectAsync();
        await clientConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);

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
            catch
            {
                // Keep the original failure if the service-side disconnect reason never arrives.
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

        using var serviceConnection = CreateSessionConnection();
        using var clientConnection = CreateSessionConnection();

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

        var exception = await Assert.ThrowsAnyAsync<DBusException>(() =>
            clientConnection.CallMethodAsync(wrongInterfaceMessage).WaitAsync(SessionBusTimeout));

        Assert.Equal("org.freedesktop.DBus.Error.UnknownMethod", exception.ErrorName);
        Assert.Equal((0, 0), position);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_TrackerService_ShouldRejectInvalidSignatureWithoutInvokingCallbacks()
    {
        var position = (X: 0, Y: 0);

        using var serviceConnection = CreateSessionConnection();
        using var clientConnection = CreateSessionConnection();

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

        var exception = await Assert.ThrowsAnyAsync<DBusException>(() =>
            clientConnection.CallMethodAsync(invalidSignatureMessage).WaitAsync(SessionBusTimeout));

        Assert.Equal("org.freedesktop.DBus.Error.InvalidArgs", exception.ErrorName);
        Assert.Equal((0, 0), position);
    }

    [DbusSessionFact]
    public async Task DbusIntegration_GnomeExtensionsClient_ShouldSendUuidAndParseReply()
    {
        const string expectedUuid = "crossmacro@zynix.net";
        string? receivedUuid = null;

        using var serviceConnection = CreateSessionConnection();
        using var clientConnection = CreateSessionConnection();

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
    public async Task DbusIntegration_KWinScriptingClient_ShouldSendScriptNameForUnload()
    {
        const string expectedScriptName = "42";
        string? receivedScriptName = null;

        using var serviceConnection = CreateSessionConnection();
        using var clientConnection = CreateSessionConnection();

        await serviceConnection.ConnectAsync();
        await clientConnection.ConnectAsync().AsTask().WaitAsync(SessionBusTimeout);

        serviceConnection.AddMethodHandler(new RecordingMethodHandler(
            KWinScriptingClient.Path,
            KWinScriptingClient.Interface,
            "unloadScript",
            request =>
            {
                receivedScriptName = request.GetBodyReader().ReadString();
            },
            replySignature: null,
            writeReply: null));

        await serviceConnection.RequestNameAsync(KWinScriptingClient.Service, RequestNameOptions.Default)
            .WaitAsync(SessionBusTimeout);

        var client = new KWinScriptingClient(clientConnection);
        await client.UnloadScriptAsync(expectedScriptName).WaitAsync(SessionBusTimeout);

        Assert.Equal(expectedScriptName, receivedScriptName);
    }

    private delegate void ReplyWriter(ref MessageWriter writer);

    private sealed class RecordingMethodHandler : IPathMethodHandler
    {
        private readonly string _expectedInterface;
        private readonly string _expectedMember;
        private readonly Action<Message> _onRequest;
        private readonly string? _replySignature;
        private readonly ReplyWriter? _writeReply;

        public RecordingMethodHandler(
            string path,
            string expectedInterface,
            string expectedMember,
            Action<Message> onRequest,
            string? replySignature,
            ReplyWriter? writeReply)
        {
            Path = path;
            _expectedInterface = expectedInterface;
            _expectedMember = expectedMember;
            _onRequest = onRequest;
            _replySignature = replySignature;
            _writeReply = writeReply;
        }

        public string Path { get; }

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
#pragma warning restore CS0618
