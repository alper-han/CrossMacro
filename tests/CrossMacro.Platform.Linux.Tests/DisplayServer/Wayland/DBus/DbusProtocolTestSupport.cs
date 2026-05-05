using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using CrossMacro.TestInfrastructure;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

internal static class DbusWrapperProtocolTestHelpers
{
    internal static Message CreateBodyOnlyMessage(byte[] body)
    {
        var message = (Message)RuntimeHelpers.GetUninitializedObject(typeof(Message));
        return AttachBody(message, body);
    }

    internal static byte[] EncodeInt32Body(int value)
    {
        var bytes = new List<byte>(4);
        WriteInt32(bytes, value);
        return bytes.ToArray();
    }

    internal static byte[] EncodeStringBody(string value)
    {
        var bytes = new List<byte>();
        WriteString(bytes, value);
        return bytes.ToArray();
    }

    internal static byte[] EncodeLayoutTriplesBody(params (string ShortName, string Variant, string DisplayName)[] layouts)
    {
        var bytes = new List<byte>();
        int lengthOffset = ReserveUInt32(bytes);
        Align(bytes, 8);
        int contentStart = bytes.Count;

        foreach (var layout in layouts)
        {
            Align(bytes, 8);
            WriteString(bytes, layout.ShortName);
            WriteString(bytes, layout.Variant);
            WriteString(bytes, layout.DisplayName);
        }

        PatchUInt32(bytes, lengthOffset, (uint)(bytes.Count - contentStart));
        return bytes.ToArray();
    }

    internal static byte[] EncodeStringVariantDictionaryBody(params (string Key, object Value)[] entries)
    {
        var bytes = new List<byte>();
        int lengthOffset = ReserveUInt32(bytes);
        Align(bytes, 8);
        int contentStart = bytes.Count;

        foreach (var entry in entries)
        {
            Align(bytes, 8);
            WriteString(bytes, entry.Key);
            WriteVariant(bytes, entry.Value);
        }

        PatchUInt32(bytes, lengthOffset, (uint)(bytes.Count - contentStart));
        return bytes.ToArray();
    }

    private static object UnboxVariant(object? value)
    {
        return value switch
        {
            VariantValue variantValue => UnboxVariant(variantValue),
            null => string.Empty,
            _ => value
        };
    }

    private static object UnboxVariant(VariantValue value)
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
        return value.Type switch
        {
            VariantValueType.Array when value.ItemType == VariantValueType.String => value.GetArray<string>(),
            VariantValueType.Array when value.ItemType == VariantValueType.ObjectPath => value.GetArray<string>(),
            VariantValueType.Array when value.ItemType == VariantValueType.Int32 => value.GetArray<int>(),
            VariantValueType.Array when value.ItemType == VariantValueType.UInt32 => value.GetArray<uint>(),
            VariantValueType.Array when value.ItemType == VariantValueType.Bool => value.GetArray<bool>(),
            VariantValueType.Array when value.ItemType == VariantValueType.Byte => value.GetArray<byte>(),
            VariantValueType.Array when value.ItemType == VariantValueType.Int64 => value.GetArray<long>(),
            VariantValueType.Array when value.ItemType == VariantValueType.UInt64 => value.GetArray<ulong>(),
            VariantValueType.Array when value.ItemType == VariantValueType.Double => value.GetArray<double>(),
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

    private static void WriteVariant(List<byte> bytes, object value)
    {
        switch (value)
        {
            case string text:
                WriteSignature(bytes, "s");
                WriteString(bytes, text);
                break;
            case bool flag:
                WriteSignature(bytes, "b");
                WriteUInt32(bytes, flag ? 1u : 0u);
                break;
            case uint unsigned:
                WriteSignature(bytes, "u");
                WriteUInt32(bytes, unsigned);
                break;
            default:
                throw new NotSupportedException($"Unsupported variant type: {value.GetType().FullName}");
        }
    }

    private static void WriteSignature(List<byte> bytes, string value)
    {
        bytes.Add((byte)value.Length);
        bytes.AddRange(Encoding.ASCII.GetBytes(value));
        bytes.Add(0);
    }

    private static void WriteString(List<byte> bytes, string value)
    {
        Align(bytes, 4);
        var encoded = Encoding.UTF8.GetBytes(value);
        bytes.AddRange(BitConverter.GetBytes((uint)encoded.Length));
        bytes.AddRange(encoded);
        bytes.Add(0);
    }

    private static void WriteInt32(List<byte> bytes, int value)
    {
        Align(bytes, 4);
        bytes.AddRange(BitConverter.GetBytes(value));
    }

    private static void WriteUInt32(List<byte> bytes, uint value)
    {
        Align(bytes, 4);
        bytes.AddRange(BitConverter.GetBytes(value));
    }

    private static int ReserveUInt32(List<byte> bytes)
    {
        Align(bytes, 4);
        int offset = bytes.Count;
        bytes.AddRange([0, 0, 0, 0]);
        return offset;
    }

    private static void PatchUInt32(List<byte> bytes, int offset, uint value)
    {
        var encoded = BitConverter.GetBytes(value);
        for (int i = 0; i < encoded.Length; i++)
        {
            bytes[offset + i] = encoded[i];
        }
    }

    private static void Align(List<byte> bytes, int alignment)
    {
        int padding = ((alignment - (bytes.Count % alignment)) % alignment);
        for (int i = 0; i < padding; i++)
        {
            bytes.Add(0);
        }
    }

    private static Message AttachBody(Message message, byte[] body)
    {
        var bodyField = typeof(Message).GetField("_body", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var isBigEndianField = typeof(Message).GetField("<IsBigEndian>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var unixFdCountField = typeof(Message).GetField("<UnixFdCount>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;

        bodyField.SetValue(message, new ReadOnlySequence<byte>(body));
        isBigEndianField.SetValue(message, false);
        unixFdCountField.SetValue(message, 0);
        return message;
    }

}

[CollectionDefinition(nameof(DbusIntegrationSerialCollection), DisableParallelization = true)]
public sealed class DbusIntegrationSerialCollection
{
}

public abstract class DbusIntegrationTestBase
{
    protected static readonly TimeSpan SessionBusTimeout = TimeSpan.FromSeconds(5);

    protected static DBusConnection CreateSessionConnection()
    {
        return new DBusConnection(
            GetSessionBusAddress()
            ?? throw new InvalidOperationException("D-Bus session bus address is unavailable after the test guard passed."));
    }

    private static string? GetSessionBusAddress()
        => DBusAddress.Session;
}
