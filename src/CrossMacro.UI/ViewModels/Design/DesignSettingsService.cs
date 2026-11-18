
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignSettingsService(AppSettings settings) : ISettingsService
{
    public AppSettings Current { get; } = settings ?? throw new ArgumentNullException(nameof(settings));

    public T AccessCurrent<T>(Func<AppSettings, T> access) => access(Current);

    public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

    public Task<AppSettings> EnsureLoadedAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public Task SaveAsync() => Task.CompletedTask;

    public Task SaveAfterIdleAsync() => Task.CompletedTask;

    public Task FlushPendingSaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ReloadAsync(string profileConfigDirectory) => Task.CompletedTask;
}
