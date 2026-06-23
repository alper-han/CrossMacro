using System;

namespace CrossMacro.Platform.Abstractions;

public readonly record struct ScreenReadResult<T>
{
    private ScreenReadResult(T? value, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        Value = value;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public T? Value { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static ScreenReadResult<T> Success(T value) => new(value, null, null);

    public static ScreenReadResult<T> Failure(ScreenReadErrorKind errorKind, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Screen read failures require a message.", nameof(errorMessage));
        }

        return new(default, errorKind, errorMessage);
    }
}
