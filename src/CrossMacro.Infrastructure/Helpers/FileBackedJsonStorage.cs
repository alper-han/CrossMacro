using System.IO;
using System.Text;
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
        var temporaryPath = GetTemporaryPath(filePath);
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            Replace(filePath, temporaryPath);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    public static async Task WriteAsync<T>(string filePath, T value, JsonTypeInfo<T> typeInfo)
    {
        EnsureParentDirectory(filePath);
        var json = JsonSerializer.Serialize(value, typeInfo);
        var temporaryPath = GetTemporaryPath(filePath);
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
            {
                await writer.WriteAsync(json).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                stream.Flush(true);
            }

            Replace(filePath, temporaryPath);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string GetTemporaryPath(string filePath) => $"{filePath}.{System.Guid.NewGuid():N}.tmp";

    private static void Replace(string filePath, string temporaryPath)
    {
        if (File.Exists(filePath))
        {
            File.Replace(temporaryPath, filePath, null);
        }
        else
        {
            File.Move(temporaryPath, filePath);
        }
    }

    private static void DeleteTemporaryFile(string temporaryPath)
    {
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }
    }
}
