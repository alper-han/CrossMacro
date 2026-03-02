using System.Collections.Generic;

namespace CrossMacro.UI.Services;

public interface IThemeService
{
    IReadOnlyList<string> AvailableThemes { get; }
    string CurrentTheme { get; }
    bool TryApplyTheme(string themeName, out string error);
}
