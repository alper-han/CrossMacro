
namespace CrossMacro.UI.Localization;

public sealed class LocExtension(string key) : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return ((Avalonia.Application.Current as App)?.LocalizationBindings ?? new LocalizationBindingSource()).Observe(Key).ToBinding();
    }
}
