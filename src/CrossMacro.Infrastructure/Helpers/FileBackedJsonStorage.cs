using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace CrossMacro.Infrastructure.Helpers;

internal static class FileBackedJsonStorage
{
    public static T? Read<T>(string filePath, JsonTypeInfo<T> typeInfo)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    public static async Task<T?> ReadAsync<T>(string filePath, JsonTypeInfo<T> typeInfo)
    {
        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    public static void Write<T>(string filePath, T value, JsonTypeInfo<T> typeInfo)
    {
        EnsureParentDirectory(filePath);
        var json = JsonSerializer.Serialize(value, typeInfo);
        File.WriteAllText(filePath, json);
    }

    public static async Task WriteAsync<T>(string filePath, T value, JsonTypeInfo<T> typeInfo)
    {
        EnsureParentDirectory(filePath);
        var json = JsonSerializer.Serialize(value, typeInfo);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
