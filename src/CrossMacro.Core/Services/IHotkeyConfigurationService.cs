using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

public interface IHotkeyConfigurationService
{
    HotkeySettings Load();
    Task<HotkeySettings> LoadAsync();
    void Save(HotkeySettings settings);
}
