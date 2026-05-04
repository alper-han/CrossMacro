using System;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace CrossMacro.UI.Icons;

public sealed class AppIconExtension : MarkupExtension
{
    public AppIconExtension()
    {
    }

    public AppIconExtension(AppIcon icon)
    {
        Icon = icon;
    }

    public AppIcon Icon { get; set; } = AppIcon.Info;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return AppIcons.Get(Icon);
    }
}
