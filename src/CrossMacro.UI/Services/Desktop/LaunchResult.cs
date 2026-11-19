
namespace CrossMacro.UI.Services.Desktop;

internal readonly record struct LaunchResult(bool Success, Exception? Failure)
{
    public static LaunchResult Succeeded { get; } = new(Success: true, Failure: null);

    public static LaunchResult Failed(Exception failure)
    {
        return new LaunchResult(Success: false, failure ?? throw new ArgumentNullException(nameof(failure)));
    }
}
