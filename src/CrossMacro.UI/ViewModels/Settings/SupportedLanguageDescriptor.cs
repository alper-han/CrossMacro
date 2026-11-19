namespace CrossMacro.UI.ViewModels.Settings;

internal sealed record SupportedLanguageDescriptor
{
    public SupportedLanguageDescriptor(
        string code,
        string resourceKey,
        string englishName,
        bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(englishName);

        Code = code;
        ResourceKey = resourceKey;
        EnglishName = englishName;
        IsDefault = isDefault;
    }

    public string Code { get; }
    public string ResourceKey { get; }
    public string EnglishName { get; }
    public bool IsDefault { get; }
}
