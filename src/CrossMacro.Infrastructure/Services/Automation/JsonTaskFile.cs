namespace CrossMacro.Infrastructure.Services.Automation;

/// <summary>
/// The profile-scoped JSON file behind one task repository. A file that cannot be parsed is moved aside and
/// treated as absent, so it can never be overwritten by the next save. Failures to read the file are not
/// swallowed.
/// </summary>
internal sealed class JsonTaskFile<TTask>
{
    private readonly string _fileName;
    private readonly JsonTypeInfo<List<TTask>> _typeInfo;
    private readonly Func<string, List<TTask>?> _parse;
    private string _filePath;

    public JsonTaskFile(
        string filePath,
        string fileName,
        JsonTypeInfo<List<TTask>> typeInfo,
        Func<string, List<TTask>?>? parse = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _fileName = fileName;
        _typeInfo = typeInfo;
        _parse = parse ?? (json => JsonSerializer.Deserialize(json, typeInfo));
    }

    public void SetProfileDirectory(string profileConfigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        _filePath = Path.Combine(profileConfigDirectory, _fileName);
    }

    /// <returns>The stored tasks, or null when the file is absent or was malformed and moved aside.</returns>
    public async Task<List<TTask>?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = _filePath;
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            return _parse(json);
        }
        catch (JsonException ex)
        {
            var backupPath = FileBackedJsonStorage.MoveAsideCorruptFile(path);
            Log.LogError(ex, "Task file {Path} is malformed; it was kept as {BackupPath} and an empty list is used", path, backupPath);
            return null;
        }
    }

    public Task SaveAsync(IEnumerable<TTask> tasks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();
        return FileBackedJsonStorage.WriteAsync(_filePath, tasks.ToList(), _typeInfo, cancellationToken);
    }
}
