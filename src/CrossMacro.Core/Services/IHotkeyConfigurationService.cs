using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

public interface IHotkeyConfigurationService
{
    HotkeyConfigurationSaveRequest CaptureSaveRequest(HotkeySettings settings);
    HotkeySettings Load();
    Task<HotkeySettings> LoadAsync();
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    void Save(HotkeySettings settings);
    bool TrySave(HotkeyConfigurationSaveRequest request);
}
