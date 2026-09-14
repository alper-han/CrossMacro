namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal interface IGnomeScreenReadingReadiness
{
    public bool IsSession { get; }
    public bool IsAvailable { get; }
    public Task Initialization { get; }
    public event EventHandler? Changed;
}
