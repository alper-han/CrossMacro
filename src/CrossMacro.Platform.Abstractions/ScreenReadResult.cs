namespace CrossMacro.Platform.Abstractions;

public readonly record struct ScreenReadResult<T>
{
    internal ScreenReadResult(T? value, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        Value = value;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public T? Value { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }
}
