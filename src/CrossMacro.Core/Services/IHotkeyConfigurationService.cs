using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

public interface IHotkeyConfigurationService
{
    HotkeySettings Load();
    void Save(HotkeySettings settings);
}
