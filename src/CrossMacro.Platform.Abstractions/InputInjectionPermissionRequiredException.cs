
namespace CrossMacro.Platform.Abstractions;

public sealed class InputInjectionPermissionRequiredException : InvalidOperationException
{
    public InputInjectionPermissionRequiredException()
    {
    }

    public InputInjectionPermissionRequiredException(string? message)
        : base(message)
    {
    }

    public InputInjectionPermissionRequiredException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
