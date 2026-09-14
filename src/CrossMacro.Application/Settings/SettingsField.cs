namespace CrossMacro.Application.Settings;

internal abstract class SettingsField(string key)
{
    public string Key { get; } = key;
    public abstract bool Equal(AppSettings left, AppSettings right);
    public abstract void Copy(AppSettings source, AppSettings destination);
}
