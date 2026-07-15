
namespace CrossMacro.UI.Localization;

public sealed class LocExtension(string key) : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return LocalizationBindingSource.Instance.Observe(Key).ToBinding();
    }
}
