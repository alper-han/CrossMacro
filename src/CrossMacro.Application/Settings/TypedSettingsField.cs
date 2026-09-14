namespace CrossMacro.Application.Settings;

internal sealed class TypedSettingsField<T>(string key, Func<AppSettings, T> read, Action<AppSettings, T> write, Func<T, T, bool>? equals = null) : SettingsField(key)
{
    public override bool Equal(AppSettings left, AppSettings right) =>
        (equals ?? EqualityComparer<T>.Default.Equals)(read(left), read(right));
    public override void Copy(AppSettings source, AppSettings destination) => write(destination, read(source));
}
