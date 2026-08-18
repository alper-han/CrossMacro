
namespace CrossMacro.Core.Services;

public interface IHotkeyConfigurationService
{
    public HotkeyConfigurationSaveRequest CaptureSaveRequest(HotkeySettings settings);
    public HotkeySettings Load();
    public Task<HotkeySettings> LoadAsync();
    public Task<HotkeySettings> ReloadAsync(string profileConfigDirectory) => LoadAsync();
    public void Save(HotkeySettings settings);
    public bool TrySave(HotkeyConfigurationSaveRequest request);
    public Task<bool> TrySaveAsync(HotkeyConfigurationSaveRequest request);
}
