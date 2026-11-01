namespace CrossMacro.UI.Themes;

public interface ISystemThemePreferenceService : IDisposable
{
    public void Start(ThemePreferenceMode mode);
    public void SetMode(ThemePreferenceMode mode);
    public event EventHandler<string>? ThemeResolved;
}
