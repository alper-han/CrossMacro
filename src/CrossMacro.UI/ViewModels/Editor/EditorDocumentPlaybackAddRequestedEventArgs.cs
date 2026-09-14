namespace CrossMacro.UI.ViewModels.Editor;

public sealed class EditorDocumentPlaybackAddRequestedEventArgs(
    EditorViewModel document,
    EditorMacroPlaybackRequestedEventArgs playbackRequested) : EventArgs
{
    public EditorViewModel Document { get; } = document ?? throw new ArgumentNullException(nameof(document));

    public EditorMacroPlaybackRequestedEventArgs PlaybackRequested { get; } = playbackRequested ?? throw new ArgumentNullException(nameof(playbackRequested));
}
