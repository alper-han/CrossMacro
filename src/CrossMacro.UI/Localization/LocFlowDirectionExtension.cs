using Avalonia.Markup.Xaml;

namespace CrossMacro.UI.Localization;

/// <summary>Binds <c>FlowDirection</c> to the reading direction of the active language.</summary>
public sealed class LocFlowDirectionExtension : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return ((Avalonia.Application.Current as App)?.LocalizationBindings ?? new LocalizationBindingSource()).ObserveFlowDirection().ToBinding();
    }
}
