
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignSettingsService(AppSettings settings) : ISettingsService
{
    public AppSettings Current { get; } = settings ?? throw new ArgumentNullException(nameof(settings));

    public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

    public AppSettings Load() => Current;

    public Task SaveAsync() => Task.CompletedTask;

    public void Save() { /* Empty */ }
}
