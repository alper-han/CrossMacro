namespace CrossMacro.Application.Settings;

public sealed record SettingsChangeRequest(AppSettings Before, AppSettings After);
