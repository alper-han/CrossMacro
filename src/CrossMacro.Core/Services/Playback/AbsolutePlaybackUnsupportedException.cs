
namespace CrossMacro.Core.Services.Playback;

public sealed class AbsolutePlaybackUnsupportedException : InvalidOperationException
{
    public AbsolutePlaybackUnsupportedException()
        : this("Unknown") { /* Empty */ }

    public AbsolutePlaybackUnsupportedException(string providerName)
        : base($"Macro contains absolute mouse coordinates, but input simulator '{providerName}' does not support absolute coordinate playback in this session. Load and edit the macro if needed, or use a backend/session with absolute coordinate support before playing it.")
    {
        ProviderName = providerName;
    }

    public AbsolutePlaybackUnsupportedException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        ProviderName = "Unknown";
    }

    public string ProviderName { get; }
}
