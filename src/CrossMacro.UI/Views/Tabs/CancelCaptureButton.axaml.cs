
namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Cancels the capture that is in progress on the editor document in the data context.
/// Callers bind <c>IsVisible</c> to the matching <c>IsCapturing…</c> flag.
/// </summary>
public partial class CancelCaptureButton : UserControl
{
    public static readonly StyledProperty<bool> IconOnlyProperty =
        AvaloniaProperty.Register<CancelCaptureButton, bool>(nameof(IconOnly));

    public CancelCaptureButton()
    {
        InitializeComponent();
    }

    /// <summary>Shows only the icon; the label is still available as the tooltip.</summary>
    public bool IconOnly
    {
        get => GetValue(IconOnlyProperty);
        set => SetValue(IconOnlyProperty, value);
    }
}
