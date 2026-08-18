
namespace CrossMacro.Platform.Abstractions;

public static class ScreenReadResultFactory
{
    public static ScreenReadResult<T> Success<T>(T value) => new(value, errorKind: null, errorMessage: null);

    public static ScreenReadResult<T> Failure<T>(ScreenReadErrorKind errorKind, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Screen read failures require a message.", nameof(errorMessage));
        }

        return new ScreenReadResult<T>(default, errorKind, errorMessage);
    }
}
