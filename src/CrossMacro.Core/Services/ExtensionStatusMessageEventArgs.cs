namespace CrossMacro.Core.Services;

public sealed class ExtensionStatusMessageEventArgs(string message) : EventArgs
{
    public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));
}
