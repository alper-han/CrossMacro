
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal abstract class LinuxDbusClientBase(DBusConnection connection, string serviceName, string objectPath, string interfaceName)
{
    protected DBusConnection Connection { get; } = connection
        ?? throw new ArgumentNullException(nameof(connection));

    protected string ServiceName { get; } = serviceName;

    protected string ObjectPath { get; } = objectPath;

    protected string InterfaceName { get; } = interfaceName;

    private MessageBuffer CreateMethodCall(string member, string? signature = null, MessageWriterAction? writeBody = null)
    {
        var writer = Connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: ServiceName,
            path: ObjectPath,
            @interface: InterfaceName,
            member: member,
            signature: signature);

        writeBody?.Invoke(ref writer);
        return writer.CreateMessage();
    }

    protected Task CallAsync(string member, string? signature = null, MessageWriterAction? writeBody = null)
        => Connection.CallMethodAsync(CreateMethodCall(member, signature, writeBody));

    protected Task<TResult> CallAsync<TResult>(string member, MessageValueReader<TResult> reader, string? signature = null, MessageWriterAction? writeBody = null)
        => Connection.CallMethodAsync(CreateMethodCall(member, signature, writeBody), reader);
}
