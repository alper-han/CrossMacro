namespace CrossMacro.UI.Services.Desktop;

internal interface ITrayIconHandle : IDisposable
{
    public event EventHandler? Clicked;
    public event EventHandler? AvailabilityChanged;
    public bool IsVisible { get; set; }
    public bool HasNativeMenuExporter { get; }
    public bool IsReady { get; }
    public Task WaitForReadyAsync(CancellationToken cancellationToken);
    public string? GetToolTipText();
    public void SetToolTipText(string? value);
}
