
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignSettingsService : ISettingsService
{
    public DesignSettingsService(AppSettings settings)
    {
        Current = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public AppSettings Current { get; }

    public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

    public AppSettings Load() => Current;

    public Task SaveAsync() => Task.CompletedTask;

    public void Save()
    {
    }
}
