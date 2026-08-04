namespace CrossMacro.Core.Services.Playback;

public sealed class LogicalRelativePositionUnavailableException : InvalidOperationException
{
    private const string DefaultMessage =
        "Logical relative playback requires a known cursor position. Add an absolute move before it, enable the initial corner reset, use a supported position provider, or use raw relative coordinates explicitly.";

    public LogicalRelativePositionUnavailableException()
        : base(DefaultMessage)
    {
    }

    public LogicalRelativePositionUnavailableException(string message)
        : base(message)
    {
    }

    public LogicalRelativePositionUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
