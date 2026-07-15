
namespace CrossMacro.Core.Services;

public interface IHotkeyConfigurationService
{
    public HotkeyConfigurationSaveRequest CaptureSaveRequest(HotkeySettings settings);
    public HotkeySettings Load();
    public Task<HotkeySettings> LoadAsync();
    public Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    public void Save(HotkeySettings settings);
    public bool TrySave(HotkeyConfigurationSaveRequest request);
}
