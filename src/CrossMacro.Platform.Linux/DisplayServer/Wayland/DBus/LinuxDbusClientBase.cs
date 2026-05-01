using System;
using System.Collections.Generic;
using System.Linq;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal delegate void MessageWriterAction(ref MessageWriter writer);

internal abstract class LinuxDbusClientBase
{
    private readonly DBusConnection _connection;

    protected LinuxDbusClientBase(DBusConnection connection, string serviceName, string objectPath, string interfaceName)
    {
        _connection = connection;
        ServiceName = serviceName;
        ObjectPath = objectPath;
        InterfaceName = interfaceName;
    }

    protected string ServiceName { get; }

    protected string ObjectPath { get; }

    protected string InterfaceName { get; }

    protected MessageBuffer CreateMethodCall(string member, string? signature = null, MessageWriterAction? writeBody = null)
    {
        var writer = _connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: ServiceName,
            path: ObjectPath,
            @interface: InterfaceName,
            member: member,
            signature: signature);

        writeBody?.Invoke(ref writer);
        return writer.CreateMessage();
    }

    protected MessageBuffer CreateMethodCallByRef(string member, string? signature = null, MessageWriterAction? writeBody = null)
        => CreateMethodCall(member, signature, writeBody);

    protected Task CallAsync(string member, string? signature = null, MessageWriterAction? writeBody = null)
        => _connection.CallMethodAsync(CreateMethodCall(member, signature, writeBody));

    protected Task CallAsyncByRef(string member, string? signature = null, MessageWriterAction? writeBody = null)
        => _connection.CallMethodAsync(CreateMethodCallByRef(member, signature, writeBody));

    protected Task<TResult> CallAsync<TResult>(string member, MessageValueReader<TResult> reader, string? signature = null, MessageWriterAction? writeBody = null)
        => _connection.CallMethodAsync(CreateMethodCall(member, signature, writeBody), reader);

    protected static object UnboxVariant(VariantValue value)
    {
        return value.Type switch
        {
            VariantValueType.Byte => value.GetByte(),
            VariantValueType.Bool => value.GetBool(),
            VariantValueType.Int16 => value.GetInt16(),
            VariantValueType.UInt16 => value.GetUInt16(),
            VariantValueType.Int32 => value.GetInt32(),
            VariantValueType.UInt32 => value.GetUInt32(),
            VariantValueType.Int64 => value.GetInt64(),
            VariantValueType.UInt64 => value.GetUInt64(),
            VariantValueType.Double => value.GetDouble(),
            VariantValueType.String => value.GetString(),
            VariantValueType.ObjectPath => value.GetObjectPathAsString(),
            VariantValueType.Signature => value.GetSignature().ToString(),
            VariantValueType.Array => UnboxArray(value),
            VariantValueType.Struct => UnboxStruct(value),
            VariantValueType.Dictionary => UnboxDictionary(value),
            VariantValueType.Variant => UnboxVariant(value.GetVariantValue()),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static object UnboxArray(VariantValue value)
    {
        return value.ItemType switch
        {
            VariantValueType.String => value.GetArray<string>(),
            VariantValueType.ObjectPath => value.GetArray<string>(),
            VariantValueType.Int32 => value.GetArray<int>(),
            VariantValueType.UInt32 => value.GetArray<uint>(),
            VariantValueType.Bool => value.GetArray<bool>(),
            VariantValueType.Byte => value.GetArray<byte>(),
            VariantValueType.Int64 => value.GetArray<long>(),
            VariantValueType.UInt64 => value.GetArray<ulong>(),
            VariantValueType.Double => value.GetArray<double>(),
            _ => Enumerable.Range(0, value.Count).Select(i => UnboxVariant(value.GetItem(i))).ToArray()
        };
    }

    private static object UnboxStruct(VariantValue value)
    {
        return value.Count switch
        {
            0 => Array.Empty<object>(),
            2 => (UnboxVariant(value.GetItem(0)), UnboxVariant(value.GetItem(1))),
            3 => (UnboxVariant(value.GetItem(0)), UnboxVariant(value.GetItem(1)), UnboxVariant(value.GetItem(2))),
            _ => Enumerable.Range(0, value.Count).Select(i => UnboxVariant(value.GetItem(i))).ToArray()
        };
    }

    private static object UnboxDictionary(VariantValue value)
    {
        var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
        for (int i = 0; i < value.Count; i++)
        {
            var entry = value.GetDictionaryEntry(i);
            dictionary[entry.Key.GetString()] = UnboxVariant(entry.Value);
        }

        return dictionary;
    }
}
