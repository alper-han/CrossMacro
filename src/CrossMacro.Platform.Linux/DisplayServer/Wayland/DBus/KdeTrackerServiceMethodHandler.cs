using System;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KdeTrackerServiceMethodHandler : IPathMethodHandler
{
    internal enum DispatchResult
    {
        Handled,
        UnknownMethod,
        InvalidArguments
    }

    private static readonly ReadOnlyMemory<byte> InterfaceXml =
        """
        <interface name="io.github.alper_han.crossmacro.Tracker">
          <method name="UpdatePosition">
            <arg direction="in" type="i"/>
            <arg direction="in" type="i"/>
          </method>
          <method name="UpdateResolution">
            <arg direction="in" type="i"/>
            <arg direction="in" type="i"/>
          </method>
          <method name="ReportWindowData">
            <arg direction="in" type="s"/>
            <arg direction="in" type="s"/>
          </method>
        </interface>

        """u8.ToArray();

    private readonly KdeTrackerService _service;

    public KdeTrackerServiceMethodHandler(KdeTrackerService service)
    {
        _service = service;
    }

    public string Path => _service.ObjectPath.ToString();

    public bool HandlesChildPaths => false;

    internal DispatchResult TryDispatchMethod(Message request)
        => TryDispatchMethod(
            request.InterfaceIsSet ? request.InterfaceAsString : null,
            request.MemberAsString ?? string.Empty,
            request.SignatureIsSet ? request.SignatureAsString : null,
            request);

    internal DispatchResult TryDispatchMethod(string? interfaceName, string member, string? signature, Message request)
    {
        if (!HasExpectedInterface(interfaceName))
        {
            return DispatchResult.UnknownMethod;
        }

        if (!HasExpectedSignature(member, signature))
        {
            return IsTrackedMember(member) ? DispatchResult.InvalidArguments : DispatchResult.UnknownMethod;
        }

        var reader = request.GetBodyReader();

        switch (member)
        {
            case KdeTrackerService.UpdatePositionMethod:
            {
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                _service.UpdatePositionAsync(x, y).GetAwaiter().GetResult();
                return DispatchResult.Handled;
            }
            case KdeTrackerService.UpdateResolutionMethod:
            {
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();
                _service.UpdateResolutionAsync(width, height).GetAwaiter().GetResult();
                return DispatchResult.Handled;
            }
            case KdeTrackerService.ReportWindowDataMethod:
            {
                string correlationId = reader.ReadString();
                string json = reader.ReadString();
                _service.ReportWindowDataAsync(correlationId, json).GetAwaiter().GetResult();
                return DispatchResult.Handled;
            }
            default:
                return DispatchResult.UnknownMethod;
        }
    }

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        try
        {
            if (context.IsDBusIntrospectRequest)
            {
                context.ReplyIntrospectXml([InterfaceXml]);
                return default;
            }

            var request = context.Request;

            var dispatchResult = TryDispatchMethod(request);
            if (dispatchResult == DispatchResult.UnknownMethod)
            {
                context.ReplyUnknownMethodError();
                return default;
            }

            if (dispatchResult == DispatchResult.InvalidArguments)
            {
                context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "Tracker request arguments were invalid.");
                return default;
            }

            using var writer = context.CreateReplyWriter(null);
            context.Reply(writer.CreateMessage());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[KdeTrackerServiceMethodHandler] DBus handling failed for {Member}", context.Request.MemberAsString);
            context.ReplyError("org.freedesktop.DBus.Error.Failed", "Tracker request failed.");
        }

        return default;
    }

    private static bool HasExpectedInterface(string? interfaceName)
    {
        return string.IsNullOrEmpty(interfaceName)
            || string.Equals(interfaceName, KdeTrackerService.TrackerInterface, StringComparison.Ordinal);
    }

    private static bool HasExpectedSignature(string member, string? signature)
    {
        string? expectedSignature = member switch
        {
            KdeTrackerService.UpdatePositionMethod => "ii",
            KdeTrackerService.UpdateResolutionMethod => "ii",
            KdeTrackerService.ReportWindowDataMethod => "ss",
            _ => null
        };

        if (expectedSignature == null)
        {
            return false;
        }

        return string.Equals(signature, expectedSignature, StringComparison.Ordinal);
    }

    private static bool IsTrackedMember(string member)
    {
        return string.Equals(member, KdeTrackerService.UpdatePositionMethod, StringComparison.Ordinal)
            || string.Equals(member, KdeTrackerService.UpdateResolutionMethod, StringComparison.Ordinal)
            || string.Equals(member, KdeTrackerService.ReportWindowDataMethod, StringComparison.Ordinal);
    }
}
