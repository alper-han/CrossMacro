
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KdeTrackerServiceMethodHandler(KdeTrackerService service) : IPathMethodHandler
{
    internal enum DispatchResult
    {
        Handled,
        UnknownMethod,
        InvalidArguments,
    }

    private static readonly ReadOnlyMemory<byte> InterfaceXml =
        "<interface name=\"io.github.alper_han.crossmacro.Tracker\"><method name=\"UpdatePosition\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"UpdateResolution\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"UpdateDesktopBounds\"><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/><arg direction=\"in\" type=\"i\"/></method><method name=\"ReportWindowData\"><arg direction=\"in\" type=\"s\"/><arg direction=\"in\" type=\"s\"/></method></interface>"u8.ToArray();

    private readonly KdeTrackerService _service = service;

    public string Path => _service.ObjectPath.ToString();

    public bool HandlesChildPaths => false;

    internal ValueTask<DispatchResult> TryDispatchMethodAsync(Message request, CancellationToken cancellationToken = default)
        => TryDispatchMethodAsync(
            request.InterfaceIsSet ? request.InterfaceAsString : null,
            request.MemberAsString ?? string.Empty,
            request.SignatureIsSet ? request.SignatureAsString : null,
            request,
            cancellationToken);

    internal async ValueTask<DispatchResult> TryDispatchMethodAsync(
        string? interfaceName,
        string member,
        string? signature,
        Message request,
        CancellationToken cancellationToken = default)
    {
        if (!HasExpectedInterface(interfaceName))
        {
            Log.Warning("[KdeTrackerServiceMethodHandler] Unknown interface: '{Interface}' for member '{Member}'", interfaceName, member);
            return DispatchResult.UnknownMethod;
        }

        if (!HasExpectedSignature(member, signature))
        {
            Log.Warning("[KdeTrackerServiceMethodHandler] Invalid signature: '{Signature}' for member '{Member}'", signature, member);
            return IsTrackedMember(member) ? DispatchResult.InvalidArguments : DispatchResult.UnknownMethod;
        }

        var reader = request.GetBodyReader();

        switch (member)
        {
            case KdeTrackerService.UpdatePositionMethod:
                {
                    var (x, y) = ReadTwoNumbers(ref reader, signature);
                    await _service.UpdatePositionAsync(x, y).WaitAsync(cancellationToken).ConfigureAwait(false);
                    return DispatchResult.Handled;
                }
            case KdeTrackerService.UpdateResolutionMethod:
                {
                    var (width, height) = ReadTwoNumbers(ref reader, signature);
                    Log.Information("[KdeTrackerServiceMethodHandler] Received UpdateResolution DBus call: {Width}x{Height} (sig: {Signature})", width, height, signature);
                    await _service.UpdateResolutionAsync(width, height).WaitAsync(cancellationToken).ConfigureAwait(false);
                    return DispatchResult.Handled;
                }
            case KdeTrackerService.UpdateDesktopBoundsMethod:
                {
                    var (x, y, width, height) = ReadFourNumbers(ref reader, signature);
                    Log.Information(
                        "[KdeTrackerServiceMethodHandler] Received UpdateDesktopBounds DBus call: ({X},{Y}) {Width}x{Height} (sig: {Signature})",
                        x,
                        y,
                        width,
                        height,
                        signature);
                    await _service.UpdateDesktopBoundsAsync(x, y, width, height)
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                    return DispatchResult.Handled;
                }
            case KdeTrackerService.ReportWindowDataMethod:
                {
                    string correlationId = reader.ReadString();
                    string json = reader.ReadString();
                    await _service.ReportWindowDataAsync(correlationId, json).WaitAsync(cancellationToken).ConfigureAwait(false);
                    return DispatchResult.Handled;
                }
            default:
                Log.Warning("[KdeTrackerServiceMethodHandler] Unknown member: '{Member}'", member);
                return DispatchResult.UnknownMethod;
        }
    }

    private static (int First, int Second) ReadTwoNumbers(ref Reader reader, string? signature)
    {
        return (
            ReadNumber(ref reader, signature, index: 0),
            ReadNumber(ref reader, signature, index: 1));
    }

    private static (int X, int Y, int Width, int Height) ReadFourNumbers(ref Reader reader, string? signature)
    {
        return (
            ReadNumber(ref reader, signature, index: 0),
            ReadNumber(ref reader, signature, index: 1),
            ReadNumber(ref reader, signature, index: 2),
            ReadNumber(ref reader, signature, index: 3));
    }

    private static int ReadNumber(ref Reader reader, string? signature, int index)
    {
        return signature is not null && signature.Length > index && signature[index] is 'd'
            ? checked((int)reader.ReadDouble())
            : reader.ReadInt32();
    }

    public async ValueTask HandleMethodAsync(MethodContext context)
    {
        try
        {
            if (context.IsDBusIntrospectRequest)
            {
                context.ReplyIntrospectXml([InterfaceXml]);
                return;
            }

            var request = context.Request;

            var dispatchResult = await TryDispatchMethodAsync(request, CancellationToken.None).ConfigureAwait(false);
            if (dispatchResult is DispatchResult.UnknownMethod)
            {
                context.ReplyUnknownMethodError();
                return;
            }

            if (dispatchResult is DispatchResult.InvalidArguments)
            {
                context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "Tracker request arguments were invalid.");
                return;
            }

            using var writer = context.CreateReplyWriter(signature: null);
            context.Reply(writer.CreateMessage());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[KdeTrackerServiceMethodHandler] DBus handling failed for {Member}", context.Request.MemberAsString);
            context.ReplyError("org.freedesktop.DBus.Error.Failed", "Tracker request failed.");
        }

    }

    private static bool HasExpectedInterface(string? interfaceName)
    {
        return string.IsNullOrEmpty(interfaceName)
            || string.Equals(interfaceName, KdeTrackerService.TrackerInterface, StringComparison.Ordinal);
    }

    private static bool HasExpectedSignature(string member, string? signature)
    {
        if (string.Equals(member, KdeTrackerService.ReportWindowDataMethod, StringComparison.Ordinal))
        {
            return string.IsNullOrEmpty(signature) || string.Equals(signature, "ss", StringComparison.Ordinal);
        }

        if (string.Equals(member, KdeTrackerService.UpdatePositionMethod, StringComparison.Ordinal) ||
            string.Equals(member, KdeTrackerService.UpdateResolutionMethod, StringComparison.Ordinal))
        {
            return string.IsNullOrEmpty(signature) ||
                   string.Equals(signature, "ii", StringComparison.Ordinal) ||
                   string.Equals(signature, "dd", StringComparison.Ordinal) ||
                   string.Equals(signature, "id", StringComparison.Ordinal) ||
                   string.Equals(signature, "di", StringComparison.Ordinal);
        }

        if (string.Equals(member, KdeTrackerService.UpdateDesktopBoundsMethod, StringComparison.Ordinal))
        {
            return string.IsNullOrEmpty(signature)
                || (signature.Length is 4 && signature.All(static type => type is 'i' or 'd'));
        }

        return false;
    }

    private static bool IsTrackedMember(string member)
    {
        return string.Equals(member, KdeTrackerService.UpdatePositionMethod, StringComparison.Ordinal)
            || string.Equals(member, KdeTrackerService.UpdateResolutionMethod, StringComparison.Ordinal)
            || string.Equals(member, KdeTrackerService.UpdateDesktopBoundsMethod, StringComparison.Ordinal)
            || string.Equals(member, KdeTrackerService.ReportWindowDataMethod, StringComparison.Ordinal);
    }
}
