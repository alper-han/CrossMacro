using System;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using Tmds.DBus.Protocol;

namespace CrossMacro.UI.Linux.Services;

/// <summary>
/// Common D-Bus plumbing for one exported interface: introspection, the standard Properties interface and error replies.
/// </summary>
internal abstract class DBusInterfaceHandler(string path, string interfaceName) : IPathMethodHandler
{
    public string Path => path;
    public bool HandlesChildPaths => false;
    protected abstract ReadOnlyMemory<byte> InterfaceXml { get; }
    protected abstract string[] PropertyNames { get; }
    protected abstract void WriteProperty(ref MessageWriter writer, string property);
    protected abstract ValueTask DispatchAsync(MethodContext context);

    public async ValueTask HandleMethodAsync(MethodContext context)
    {
        try
        {
            if (context.IsDBusIntrospectRequest)
            {
                context.ReplyIntrospectXml([InterfaceXml]);
                return;
            }
            if (context.Request.InterfaceAsString is "org.freedesktop.DBus.Properties")
            {
                HandleProperties(context);
                return;
            }
            if (!string.Equals(context.Request.InterfaceAsString, interfaceName, StringComparison.Ordinal))
            {
                context.ReplyUnknownMethodError();
                return;
            }
            await DispatchAsync(context).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[LinuxTray] Request {Interface}.{Member} failed.",
                context.Request.InterfaceAsString, context.Request.MemberAsString);
            context.ReplyError("org.freedesktop.DBus.Error.Failed", "Tray request failed.");
        }
    }

    private void HandleProperties(MethodContext context)
    {
        var member = context.Request.MemberAsString;
        if (member is not ("Get" or "GetAll" or "Set")) { context.ReplyUnknownMethodError(); return; }
        if (!CheckSignature(context, member switch { "Get" => "ss", "Set" => "ssv", _ => "s" })) { return; }
        var reader = context.Request.GetBodyReader();
        if (!string.Equals(reader.ReadString(), interfaceName, StringComparison.Ordinal))
        {
            context.ReplyError("org.freedesktop.DBus.Error.UnknownInterface", "Unknown interface.");
            return;
        }
        if (member is "GetAll")
        {
            var writer = context.CreateReplyWriter("a{sv}");
            try
            {
                var array = writer.WriteDictionaryStart();
                foreach (var property in PropertyNames)
                {
                    writer.WriteDictionaryEntryStart();
                    writer.WriteString(property);
                    WriteProperty(ref writer, property);
                }
                writer.WriteDictionaryEnd(array);
                context.Reply(writer.CreateMessage());
            }
            finally { writer.Dispose(); }
            return;
        }
        var name = reader.ReadString();
        if (Array.IndexOf(PropertyNames, name) < 0)
        {
            context.ReplyError("org.freedesktop.DBus.Error.UnknownProperty", "Unknown property.");
            return;
        }
        if (member is "Set")
        {
            context.ReplyError("org.freedesktop.DBus.Error.PropertyReadOnly", "Property is read-only.");
            return;
        }
        var reply = context.CreateReplyWriter("v");
        try
        {
            WriteProperty(ref reply, name);
            context.Reply(reply.CreateMessage());
        }
        finally { reply.Dispose(); }
    }

    protected static bool CheckSignature(MethodContext context, string signature)
    {
        if (string.Equals(context.Request.SignatureAsString, signature, StringComparison.Ordinal)) { return true; }
        context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", "Unexpected method signature.");
        return false;
    }

    protected static void ReplyEmpty(MethodContext context)
    {
        using var writer = context.CreateReplyWriter(signature: null);
        context.Reply(writer.CreateMessage());
    }
}
