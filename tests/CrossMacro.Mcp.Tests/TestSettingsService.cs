namespace CrossMacro.Mcp.Tests;

internal sealed class TestSettingsService(AppSettings settings) : ISettingsService
{
    public AppSettings Current { get; } = settings;

    public T AccessCurrent<T>(Func<AppSettings, T> access) => access(Current);

    public AppSettings Load() => Current;

    public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

    public Task<AppSettings> EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public void Save()
    {
    }

    public Task SaveAsync() => Task.CompletedTask;

    public Task SaveAfterIdleAsync() => Task.CompletedTask;

    public Task FlushPendingSaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ReloadAsync(string profileConfigDirectory) => Task.CompletedTask;
}
