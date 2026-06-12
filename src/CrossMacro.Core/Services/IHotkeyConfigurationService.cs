using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

public interface IHotkeyConfigurationService
{
    HotkeySettings Load();
    Task<HotkeySettings> LoadAsync();
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    void Save(HotkeySettings settings);
}
