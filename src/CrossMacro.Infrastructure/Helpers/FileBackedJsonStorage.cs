
namespace CrossMacro.Infrastructure.Helpers;

internal static class FileBackedJsonStorage
{
    public static T? Read<T>(string filePath, JsonTypeInfo<T> typeInfo)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    public static async Task<T?> ReadAsync<T>(string filePath, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, typeInfo);
    }

    /// <summary>
    /// Renames a file that could not be parsed so a later save cannot overwrite it.
    /// </summary>
    /// <returns>The path the file was moved to.</returns>
    public static string MoveAsideCorruptFile(string filePath)
    {
        var backupPath = $"{filePath}.{DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture)}.corrupt";
        File.Move(filePath, backupPath);
        return backupPath;
    }

    public static void Write<T>(string filePath, T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        AtomicFile.Write(filePath, stream => stream.Write(json));
    }

    public static Task WriteAsync<T>(string filePath, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        return AtomicFile.WriteAsync(filePath, stream => stream.WriteAsync(json, cancellationToken).AsTask(), cancellationToken);
    }
}
