
namespace CrossMacro.Platform.Abstractions;

public interface IScreenImageAutomation
{
    public string ProviderName { get; }

    public bool IsSupported { get; }

    public Task<ScreenImageAutomationResult> SearchAsync(
        ScreenImageAutomationRequest request,
        CancellationToken cancellationToken);

    public Task<ScreenImageAutomationResult> WaitAsync(
        ScreenImageAutomationRequest request,
        CancellationToken cancellationToken);

    public Task<ScreenImageAutomationResult> ClickAsync(
        ScreenImageAutomationRequest request,
        int buttonCode,
        CancellationToken cancellationToken);
}
