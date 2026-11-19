
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

/// <summary>
/// Converts a D-Bus <see cref="VariantValue"/> tree into plain CLR objects: typed primitives, typed arrays for
/// primitive element types, value tuples for two- and three-field structs, and string-keyed dictionaries.
/// </summary>
internal static class DBusVariantUnboxer
{
    public static object Unbox(VariantValue value)
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
            VariantValueType.Variant => Unbox(value.GetVariantValue()),
            VariantValueType.Invalid or VariantValueType.UnixFd => value.ToString() ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
    }

    public static Dictionary<string, object> UnboxDictionary(VariantValue value)
    {
        var dictionary = new Dictionary<string, object>(StringComparer.Ordinal);
        for (var i = 0; i < value.Count; i++)
        {
            var entry = value.GetDictionaryEntry(i);
            dictionary[entry.Key.GetString()] = Unbox(entry.Value);
        }

        return dictionary;
    }

    private static object UnboxArray(VariantValue value)
    {
        return value.ItemType switch
        {
            VariantValueType.String or VariantValueType.ObjectPath => value.GetArray<string>(),
            VariantValueType.Int32 => value.GetArray<int>(),
            VariantValueType.UInt32 => value.GetArray<uint>(),
            VariantValueType.Bool => value.GetArray<bool>(),
            VariantValueType.Byte => value.GetArray<byte>(),
            VariantValueType.Int64 => value.GetArray<long>(),
            VariantValueType.UInt64 => value.GetArray<ulong>(),
            VariantValueType.Double => value.GetArray<double>(),
            VariantValueType.Int16
                or VariantValueType.UInt16
                or VariantValueType.Signature
                or VariantValueType.Invalid
                or VariantValueType.Array
                or VariantValueType.Struct
                or VariantValueType.Variant
                or VariantValueType.Dictionary
                or VariantValueType.UnixFd => UnboxItems(value),
            _ => UnboxItems(value),
        };
    }

    private static object UnboxStruct(VariantValue value)
    {
        return value.Count switch
        {
            0 => Array.Empty<object>(),
            2 => (Unbox(value.GetItem(0)), Unbox(value.GetItem(1))),
            3 => (Unbox(value.GetItem(0)), Unbox(value.GetItem(1)), Unbox(value.GetItem(2))),
            _ => UnboxItems(value),
        };
    }

    private static object[] UnboxItems(VariantValue value)
    {
        var items = new object[value.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = Unbox(value.GetItem(i));
        }

        return items;
    }
}
