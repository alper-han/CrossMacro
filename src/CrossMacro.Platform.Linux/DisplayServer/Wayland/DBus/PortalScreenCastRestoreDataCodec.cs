#pragma warning disable IDE0072

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal static class PortalScreenCastRestoreDataCodec
{
    private const string Prefix = "crossmacro-portal-restore-v1:";
    private const int MaxBytes = 64 * 1024;
    private const int MaxDepth = 16;
    private const int MaxNodes = 512;
    private const int MaxCollectionLength = 128;
    private const int MaxStringLength = 16 * 1024;

    private static readonly string[] SupportedIssuers = ["GNOME", "KDE", "COSMIC", "wlroots", "hyprland"];

    public static bool IsSupportedEnvelope(VariantValue value)
    {
        while (value.Type is VariantValueType.Variant)
        {
            value = value.GetVariantValue();
        }

        return value.Type is VariantValueType.Struct
            && value.Count is 3
            && value.GetItem(0).Type is VariantValueType.String
            && SupportedIssuers.Contains(value.GetItem(0).GetString(), StringComparer.Ordinal)
            && value.GetItem(1).Type is VariantValueType.UInt32
            && value.GetItem(1).GetUInt32() is > 0 and <= 3
            && value.GetItem(2).Type is VariantValueType.Variant;
    }

    public static string? TrySerialize(VariantValue value)
    {
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                var state = new CodecState();
                WriteValue(writer, value, depth: 0, state);
            }

            if (stream.Length > MaxBytes)
            {
                return null;
            }

            return Prefix + Convert.ToBase64String(stream.GetBuffer(), 0, checked((int)stream.Length));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException or InvalidOperationException or OverflowException)
        {
            return null;
        }
    }

    public static bool TryDeserialize(string? serialized, out VariantValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(serialized) || !serialized.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (serialized.Length > Prefix.Length + ((MaxBytes + 2) / 3 * 4))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(serialized[Prefix.Length..]);
            if (bytes.Length is 0 or > MaxBytes)
            {
                return false;
            }

            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var state = new CodecState();
            value = ReadValue(reader, depth: 0, state);
            return stream.Position == stream.Length;
        }
        catch (Exception ex) when (ex is ArgumentException or EndOfStreamException or InvalidDataException or IOException or InvalidOperationException or OverflowException)
        {
            value = default;
            return false;
        }
    }

    private static void WriteValue(BinaryWriter writer, VariantValue value, int depth, CodecState state)
    {
        Enter(depth, state);
        writer.Write((byte)value.Type);
        switch (value.Type)
        {
            case VariantValueType.Byte:
                writer.Write(value.GetByte());
                break;
            case VariantValueType.Bool:
                writer.Write(value.GetBool());
                break;
            case VariantValueType.Int16:
                writer.Write(value.GetInt16());
                break;
            case VariantValueType.UInt16:
                writer.Write(value.GetUInt16());
                break;
            case VariantValueType.Int32:
                writer.Write(value.GetInt32());
                break;
            case VariantValueType.UInt32:
                writer.Write(value.GetUInt32());
                break;
            case VariantValueType.Int64:
                writer.Write(value.GetInt64());
                break;
            case VariantValueType.UInt64:
                writer.Write(value.GetUInt64());
                break;
            case VariantValueType.Double:
                writer.Write(value.GetDouble());
                break;
            case VariantValueType.String:
                WriteString(writer, value.GetString());
                break;
            case VariantValueType.ObjectPath:
                WriteString(writer, value.GetObjectPathAsString());
                break;
            case VariantValueType.Signature:
                WriteString(writer, value.GetSignature().ToString());
                break;
            case VariantValueType.Variant:
                WriteValue(writer, value.GetVariantValue(), depth + 1, state);
                break;
            case VariantValueType.Struct:
                WriteCollectionLength(writer, value.Count);
                for (var i = 0; i < value.Count; i++)
                {
                    WriteValue(writer, value.GetItem(i), depth + 1, state);
                }
                break;
            case VariantValueType.Array:
                writer.Write((byte)value.ItemType);
                WriteCollectionLength(writer, value.Count);
                for (var i = 0; i < value.Count; i++)
                {
                    WriteValue(writer, value.GetItem(i), depth + 1, state);
                }
                break;
            case VariantValueType.Dictionary:
                writer.Write((byte)value.KeyType);
                writer.Write((byte)value.ValueType);
                WriteCollectionLength(writer, value.Count);
                for (var i = 0; i < value.Count; i++)
                {
                    var entry = value.GetDictionaryEntry(i);
                    WriteValue(writer, entry.Key, depth + 1, state);
                    WriteValue(writer, entry.Value, depth + 1, state);
                }
                break;
            default:
                throw new InvalidDataException($"Unsupported portal restore value type '{value.Type}'.");
        }
    }

    private static VariantValue ReadValue(BinaryReader reader, int depth, CodecState state)
    {
        Enter(depth, state);
        var type = ReadType(reader);
        return type switch
        {
            VariantValueType.Byte => VariantValue.Byte(reader.ReadByte()),
            VariantValueType.Bool => VariantValue.Bool(reader.ReadBoolean()),
            VariantValueType.Int16 => VariantValue.Int16(reader.ReadInt16()),
            VariantValueType.UInt16 => VariantValue.UInt16(reader.ReadUInt16()),
            VariantValueType.Int32 => VariantValue.Int32(reader.ReadInt32()),
            VariantValueType.UInt32 => VariantValue.UInt32(reader.ReadUInt32()),
            VariantValueType.Int64 => VariantValue.Int64(reader.ReadInt64()),
            VariantValueType.UInt64 => VariantValue.UInt64(reader.ReadUInt64()),
            VariantValueType.Double => VariantValue.Double(reader.ReadDouble()),
            VariantValueType.String => VariantValue.String(ReadString(reader)),
            VariantValueType.ObjectPath => VariantValue.ObjectPath(new ObjectPath(ReadString(reader))),
            VariantValueType.Signature => VariantValue.Signature(new Signature(Encoding.UTF8.GetBytes(ReadString(reader)))),
            VariantValueType.Variant => VariantValue.Variant(ReadValue(reader, depth + 1, state)),
            VariantValueType.Struct => ReadStruct(reader, depth, state),
            VariantValueType.Array => ReadArray(reader, depth, state),
            VariantValueType.Dictionary => ReadDictionary(reader, depth, state),
            _ => throw new InvalidDataException($"Unsupported portal restore value type '{type}'."),
        };
    }

    private static VariantValue ReadStruct(BinaryReader reader, int depth, CodecState state)
    {
        var count = ReadCollectionLength(reader);
        var fields = new VariantValue[count];
        for (var i = 0; i < count; i++)
        {
            fields[i] = ReadValue(reader, depth + 1, state);
        }

        return CreateStruct(fields);
    }

    private static VariantValue ReadArray(BinaryReader reader, int depth, CodecState state)
    {
        var itemType = ReadType(reader);
        var count = ReadCollectionLength(reader);
        var items = new VariantValue[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = ReadValue(reader, depth + 1, state);
            if (items[i].Type != itemType)
            {
                throw new InvalidDataException("Portal restore array item type mismatch.");
            }
        }

        return CreateArray(itemType, items);
    }

    private static VariantValue ReadDictionary(BinaryReader reader, int depth, CodecState state)
    {
        var keyType = ReadType(reader);
        var valueType = ReadType(reader);
        var count = ReadCollectionLength(reader);
        if (keyType is not VariantValueType.String || valueType is not VariantValueType.Variant)
        {
            throw new InvalidDataException("Unsupported portal restore dictionary type.");
        }

        var dictionary = new Dict<string, VariantValue>();
        for (var i = 0; i < count; i++)
        {
            var key = ReadValue(reader, depth + 1, state);
            var value = ReadValue(reader, depth + 1, state);
            if (key.Type is not VariantValueType.String)
            {
                throw new InvalidDataException("Portal restore dictionary item type mismatch.");
            }

            dictionary.Add(key.GetString(), value.Type is VariantValueType.Variant ? value.GetVariantValue() : value);
        }

        return dictionary;
    }

    private static VariantValue CreateStruct(IReadOnlyList<VariantValue> fields)
    {
        if (fields.Count is 3
            && fields[0].Type is VariantValueType.String
            && fields[1].Type is VariantValueType.UInt32
            && fields[2].Type is VariantValueType.Variant)
        {
            return new Struct<string, uint, VariantValue>(fields[0].GetString(), fields[1].GetUInt32(), fields[2]);
        }

        if (fields.Count is 3
            && fields[0].Type is VariantValueType.UInt32
            && fields[1].Type is VariantValueType.UInt32
            && fields[2].Type is VariantValueType.Variant)
        {
            return new Struct<uint, uint, VariantValue>(fields[0].GetUInt32(), fields[1].GetUInt32(), fields[2]);
        }

        if (fields.Count is 5
            && fields[0].Type is VariantValueType.String
            && fields[1].Type is VariantValueType.UInt32
            && fields[2].Type is VariantValueType.String
            && fields[3].Type is VariantValueType.Bool
            && fields[4].Type is VariantValueType.UInt64)
        {
            return new Struct<string, uint, string, bool, ulong>(
                fields[0].GetString(),
                fields[1].GetUInt32(),
                fields[2].GetString(),
                fields[3].GetBool(),
                fields[4].GetUInt64());
        }

        if (fields.Count is 2
            && fields[0].Type is VariantValueType.Array
            && fields[0].ItemType is VariantValueType.String
            && fields[1].Type is VariantValueType.Array
            && fields[1].ItemType is VariantValueType.String)
        {
            return new Struct<Array<string>, Array<string>>(
                new Array<string>(fields[0].GetArray<string>()),
                new Array<string>(fields[1].GetArray<string>()));
        }

        if (fields.Count is 3
            && fields[0].Type is VariantValueType.Int64
            && fields[1].Type is VariantValueType.Int64
            && fields[2].Type is VariantValueType.Array
            && fields[2].ItemType is VariantValueType.Struct)
        {
            var streams = new Array<Struct<uint, uint, VariantValue>>(fields[2].Count);
            for (var i = 0; i < fields[2].Count; i++)
            {
                var stream = fields[2].GetItem(i);
                streams.Add(new Struct<uint, uint, VariantValue>(
                    stream.GetItem(0).GetUInt32(),
                    stream.GetItem(1).GetUInt32(),
                    stream.GetItem(2)));
            }

            return new Struct<long, long, Array<Struct<uint, uint, VariantValue>>>(
                fields[0].GetInt64(),
                fields[1].GetInt64(),
                streams);
        }

        return fields.Count switch
        {
            1 => VariantValue.Struct(fields[0]),
            2 => VariantValue.Struct(fields[0], fields[1]),
            3 => VariantValue.Struct(fields[0], fields[1], fields[2]),
            4 => VariantValue.Struct(fields[0], fields[1], fields[2], fields[3]),
            5 => VariantValue.Struct(fields[0], fields[1], fields[2], fields[3], fields[4]),
            6 => VariantValue.Struct(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5]),
            7 => VariantValue.Struct(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6]),
            8 => VariantValue.Struct(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6], fields[7]),
            9 => VariantValue.Struct(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6], fields[7], fields[8]),
            10 => VariantValue.Struct(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6], fields[7], fields[8], fields[9]),
            _ => throw new InvalidDataException("Portal restore struct field count is invalid."),
        };
    }

    private static VariantValue CreateArray(VariantValueType itemType, IReadOnlyList<VariantValue> items)
    {
        return itemType switch
        {
            VariantValueType.Byte => VariantValue.Array(items.Select(static item => item.GetByte()).ToArray()),
            VariantValueType.Bool => VariantValue.Array(items.Select(static item => item.GetBool()).ToArray()),
            VariantValueType.Int16 => VariantValue.Array(items.Select(static item => item.GetInt16()).ToArray()),
            VariantValueType.UInt16 => VariantValue.Array(items.Select(static item => item.GetUInt16()).ToArray()),
            VariantValueType.Int32 => VariantValue.Array(items.Select(static item => item.GetInt32()).ToArray()),
            VariantValueType.UInt32 => VariantValue.Array(items.Select(static item => item.GetUInt32()).ToArray()),
            VariantValueType.Int64 => VariantValue.Array(items.Select(static item => item.GetInt64()).ToArray()),
            VariantValueType.UInt64 => VariantValue.Array(items.Select(static item => item.GetUInt64()).ToArray()),
            VariantValueType.Double => VariantValue.Array(items.Select(static item => item.GetDouble()).ToArray()),
            VariantValueType.String => VariantValue.Array(items.Select(static item => item.GetString()).ToArray()),
            VariantValueType.ObjectPath => VariantValue.Array(items.Select(static item => item.GetObjectPath()).ToArray()),
            VariantValueType.Signature => VariantValue.Array(items.Select(static item => item.GetSignature()).ToArray()),
            VariantValueType.Variant => VariantValue.ArrayOfVariant(items.Select(static item => item.GetVariantValue()).ToArray()),
            VariantValueType.Struct => CreateStructArray(items),
            _ => throw new InvalidDataException("Unsupported portal restore array type."),
        };
    }

    private static VariantValue CreateStructArray(IReadOnlyList<VariantValue> items)
    {
        if (items.Count is 0)
        {
            throw new InvalidDataException("Portal restore struct arrays cannot be empty.");
        }

        if (items.All(static item => item.Count is 3
            && item.GetItem(0).Type is VariantValueType.UInt32
            && item.GetItem(1).Type is VariantValueType.UInt32
            && item.GetItem(2).Type is VariantValueType.Variant))
        {
            var array = new Array<Struct<uint, uint, VariantValue>>(items.Count);
            foreach (var item in items)
            {
                array.Add(new Struct<uint, uint, VariantValue>(
                    item.GetItem(0).GetUInt32(),
                    item.GetItem(1).GetUInt32(),
                    item.GetItem(2)));
            }

            return array;
        }

        var shapes = items.Select(static item => string.Join(
            '/',
            Enumerable.Range(0, item.Count).Select(i => item.GetItem(i).Type.ToString())));
        throw new InvalidDataException($"Unsupported portal restore struct array shape: {string.Join(',', shapes)}");
    }

    private static VariantValueType ReadType(BinaryReader reader)
    {
        var raw = reader.ReadByte();
        if (!Enum.IsDefined((VariantValueType)raw) || raw is (byte)VariantValueType.Invalid or (byte)VariantValueType.UnixFd)
        {
            throw new InvalidDataException("Portal restore value type is invalid.");
        }

        return (VariantValueType)raw;
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        if (value.Length > MaxStringLength)
        {
            throw new InvalidDataException("Portal restore string is too long.");
        }

        writer.Write(value);
    }

    private static string ReadString(BinaryReader reader)
    {
        var value = reader.ReadString();
        if (value.Length > MaxStringLength)
        {
            throw new InvalidDataException("Portal restore string is too long.");
        }

        return value;
    }

    private static void WriteCollectionLength(BinaryWriter writer, int length)
    {
        if ((uint)length > MaxCollectionLength)
        {
            throw new InvalidDataException("Portal restore collection is too large.");
        }

        writer.Write(length);
    }

    private static int ReadCollectionLength(BinaryReader reader)
    {
        var length = reader.ReadInt32();
        if ((uint)length > MaxCollectionLength)
        {
            throw new InvalidDataException("Portal restore collection is too large.");
        }

        return length;
    }

    private static void Enter(int depth, CodecState state)
    {
        if (depth > MaxDepth || ++state.Nodes > MaxNodes)
        {
            throw new InvalidDataException("Portal restore data is too complex.");
        }
    }

    private sealed class CodecState
    {
        public int Nodes { get; set; }
    }
}
