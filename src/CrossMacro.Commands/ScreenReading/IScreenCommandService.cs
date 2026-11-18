
namespace CrossMacro.Commands.ScreenReading;

public interface IScreenCommandService
{
    public Task<OperationResult> ExecuteAsync(ScreenOptions options, CancellationToken cancellationToken);
}
