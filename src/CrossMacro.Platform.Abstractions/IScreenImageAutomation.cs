
namespace CrossMacro.Platform.Abstractions;

public interface IScreenImageAutomation
{
    string ProviderName { get; }

    bool IsSupported { get; }

    Task<ScreenImageAutomationResult> SearchAsync(
        ScreenImageAutomationRequest request,
        CancellationToken cancellationToken);

    Task<ScreenImageAutomationResult> WaitAsync(
        ScreenImageAutomationRequest request,
        CancellationToken cancellationToken);

    Task<ScreenImageAutomationResult> ClickAsync(
        ScreenImageAutomationRequest request,
        int buttonCode,
        CancellationToken cancellationToken);
}
