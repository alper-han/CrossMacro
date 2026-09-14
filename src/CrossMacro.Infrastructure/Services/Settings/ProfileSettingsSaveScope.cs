namespace CrossMacro.Infrastructure.Services.Settings;

/// <summary>An immutable publication identity for one profile's save destination.</summary>
internal sealed record ProfileSettingsSaveScope(string Path, bool IsReady);
