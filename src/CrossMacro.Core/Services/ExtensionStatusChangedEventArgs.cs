using System;

namespace CrossMacro.Core.Services;

public enum ExtensionStatusCode
{
    Info = 0,
    Enabled = 1,
    Warning = 2,
    Error = 3
}

public sealed class ExtensionStatusChangedEventArgs : EventArgs
{
    public ExtensionStatusChangedEventArgs(ExtensionStatusCode code, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be null or whitespace.", nameof(message));
        }

        Code = code;
        Message = message;
    }

    public ExtensionStatusCode Code { get; }

    public string Message { get; }
}
