
namespace CrossMacro.Commands.ScreenReading;

public interface IScreenshotCommandService
{
    public Task<OperationResult> ExecuteAsync(ScreenshotOptions options, CancellationToken cancellationToken);
}
