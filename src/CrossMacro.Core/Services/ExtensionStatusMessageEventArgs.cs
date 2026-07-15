namespace CrossMacro.Core.Services;

public sealed class ExtensionStatusMessageEventArgs : EventArgs
{
    public ExtensionStatusMessageEventArgs(string message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public string Message { get; }
}
